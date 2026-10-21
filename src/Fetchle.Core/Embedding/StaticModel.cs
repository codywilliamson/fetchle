using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Fetchle.Core.Embedding;

// a Model2Vec static embedding model, memory-mapped from one .fmodel file (see ModelFormat) and used in place.
// the file is validated on open and then never deserialized: lookups binary-search the mapped bytes, encoding
// sums mapped int8 rows. opened with FileShare.Delete so an updater can swap a new file in while this one stays mapped
public sealed unsafe class StaticModel : IDisposable, Fetchle.Core.Ranking.Semantic.ITextEncoder
{
    public const string FILE_NAME = "potion-retrieval-32M.fmodel";
    public const string ENV_PATH = "FETCHLE_MODEL";

    readonly MemoryMappedFile? _file;
    readonly MemoryMappedViewAccessor? _view;
    GCHandle _pinned;
    readonly byte* _base;
    readonly int _length;
    readonly ModelLayout _layout;
    bool _disposed;

    StaticModel(MemoryMappedFile? file, MemoryMappedViewAccessor? view, GCHandle pinned, byte* start, int length, ModelLayout layout)
    {
        _file = file;
        _view = view;
        _pinned = pinned;
        _base = start;
        _length = length;
        _layout = layout;
        Checksum = BinaryPrimitives.ReadUInt64LittleEndian(Image[ModelFormat.CHECKSUM_OFFSET..]);
    }

    public int Dimensions => _layout.Dims;

    // the header's xxhash3, identifies this exact model in the vector store
    public ulong Checksum { get; }

    // mean of every vocab row after L2 normalization; callers center vectors with it
    public ReadOnlySpan<float> Mean => MemoryMarshal.Cast<byte, float>(Image[_layout.Mean]);

    internal int VocabCount => _layout.Vocab;

    internal int UnkId => _layout.UnkId;

    internal int MaxWordChars => _layout.MaxWordChars;

    internal int MaxTokenBytes => _layout.MaxTokenBytes;

    internal ReadOnlySpan<byte> Prefix => _layout.Prefix;

    ReadOnlySpan<byte> Image => new(_base, _length);

    ReadOnlySpan<uint> TokenOffsets => MemoryMarshal.Cast<byte, uint>(Image[_layout.TokenOffsets]);

    ReadOnlySpan<uint> LookupOrder => MemoryMarshal.Cast<byte, uint>(Image[_layout.Lookup]);

    ReadOnlySpan<sbyte> Rows => MemoryMarshal.Cast<byte, sbyte>(Image[_layout.Embeddings]);

    // null when there's no file. any other failure is ModelUnreadableException
    public static StaticModel? Open(string path, bool verifyChecksum = true)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, bufferSize: 1);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ModelUnreadableException(path, e.Message);
        }

        MemoryMappedFile? file = null;
        MemoryMappedViewAccessor? view = null;
        var acquired = false;
        try
        {
            var length = stream.Length;
            if (length < ModelFormat.HEADER_SIZE || length > int.MaxValue)
            {
                throw new ModelUnreadableException(path, length < ModelFormat.HEADER_SIZE ? "truncated header" : "too large");
            }

            file = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
            view = file.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
            byte* start = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref start);
            acquired = true;
            start += view.PointerOffset;
            var reason = ModelChecks.Validate(new ReadOnlySpan<byte>(start, (int)length), out var layout, verifyChecksum);
            if (reason is not null)
            {
                throw new ModelUnreadableException(path, reason);
            }

            return new StaticModel(file, view, default, start, (int)length, layout);
        }
        catch (Exception e)
        {
            if (acquired)
            {
                view!.SafeMemoryMappedViewHandle.ReleasePointer();
            }

            view?.Dispose();
            file?.Dispose();
            stream.Dispose();
            if (e is ModelUnreadableException)
            {
                throw;
            }

            if (e is IOException or UnauthorizedAccessException)
            {
                throw new ModelUnreadableException(path, e.Message);
            }
            throw;
        }
    }

    // the env var wins when set (a file, or a directory that holds FILE_NAME), otherwise the file beside the exe
    public static StaticModel? FindBesideExe(bool verifyChecksum = true) => Open(PathBesideExe(), verifyChecksum);

    // where FindBesideExe looks: the env var when set (a file, or a directory that holds FILE_NAME), otherwise beside the exe
    public static string PathBesideExe() => Locate(Environment.GetEnvironmentVariable(ENV_PATH), AppContext.BaseDirectory);

    internal static StaticModel? Find(string? envPath, string exeDirectory) => Open(Locate(envPath, exeDirectory));

    static string Locate(string? envPath, string exeDirectory) =>
        !string.IsNullOrEmpty(envPath)
            ? (Directory.Exists(envPath) ? Path.Combine(envPath, FILE_NAME) : envPath)
            : Path.Combine(exeDirectory, FILE_NAME);

    // tests skip the disk. the array stays pinned until Dispose
    internal static StaticModel FromBytes(byte[] image, string source = "memory")
    {
        var reason = ModelChecks.Validate(image, out var layout);
        if (reason is not null)
        {
            throw new ModelUnreadableException(source, reason);
        }

        return FromValidatedBytes(image, layout);
    }

    // a fuzz test skips the checksum to reach the structural checks with arbitrary bytes behind them
    internal static StaticModel? FromBytesSkippingChecksum(byte[] image)
    {
        if (image.Length < ModelFormat.HEADER_SIZE || ModelChecks.ValidateStructure(image, out var layout) is not null)
        {
            return null;
        }

        return FromValidatedBytes(image, layout);
    }

    static StaticModel FromValidatedBytes(byte[] image, ModelLayout layout)
    {
        var pinned = GCHandle.Alloc(image, GCHandleType.Pinned);
        return new StaticModel(null, null, pinned, (byte*)pinned.AddrOfPinnedObject(), image.Length, layout);
    }

    // writes mean(token rows), L2-normalized, into vector (length Dimensions). false and zeros when the text has
    // no known tokens. thread-safe and allocation-free: all scratch is on the stack
    public bool Encode(ReadOnlySpan<char> text, Span<float> vector)
    {
        if (vector.Length != _layout.Dims)
        {
            throw new ArgumentException($"vector must hold {_layout.Dims} floats", nameof(vector));
        }

        Span<int> sums = stackalloc int[_layout.Dims];
        sums.Clear();
        var sink = new RowSum(Rows, _layout.Dims, sums);
        BertTokenizer.Tokenize(this, text, ref sink);
        return sink.Normalize(vector);
    }

    // the ids a text becomes, [UNK] dropped. tests and the parity check use this; Encode never builds a list
    internal int[] TokenIds(ReadOnlySpan<char> text)
    {
        var ids = new List<int>();
        var sink = new IdList(ids);
        BertTokenizer.Tokenize(this, text, ref sink);
        return [.. ids];
    }

    // the id of a token's exact utf-8 bytes, or -1
    internal int Lookup(ReadOnlySpan<byte> token)
    {
        var order = LookupOrder;
        var offsets = TokenOffsets;
        var blob = Image[_layout.TokenBlob];
        var low = 0;
        var high = order.Length - 1;
        while (low <= high)
        {
            var mid = (int)((uint)(low + high) >> 1);
            var id = (int)order[mid];
            var candidate = blob[(int)offsets[id]..(int)offsets[id + 1]];
            var comparison = candidate.SequenceCompareTo(token);
            if (comparison == 0)
            {
                return id;
            }

            if (comparison < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return -1;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_view is not null)
        {
            _view.SafeMemoryMappedViewHandle.ReleasePointer();
            _view.Dispose();
            _file!.Dispose();
        }

        if (_pinned.IsAllocated)
        {
            _pinned.Free();
        }
    }

    readonly struct IdList(List<int> ids) : ITokenSink
    {
        public void Add(int id) => ids.Add(id);
    }
}
