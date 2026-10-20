using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace Fetchle.Core.Index;

// a validated, memory-mapped snapshot. columns are spans straight over the mapping, nothing is deserialized.
// opened with FileShare.Delete so a writer can swap a new file in while this one stays mapped
sealed unsafe class Snapshot : IDisposable
{
    readonly MemoryMappedFile? _file;
    readonly MemoryMappedViewAccessor? _view;
    GCHandle _pinned;
    readonly byte* _base;
    readonly int _length;
    readonly SnapshotLayout _layout;
    readonly string[] _rootPaths;
    bool _disposed;

    Snapshot(MemoryMappedFile? file, MemoryMappedViewAccessor? view, GCHandle pinned, byte* start, int length, SnapshotLayout layout, string source)
    {
        _file = file;
        _view = view;
        _pinned = pinned;
        _base = start;
        _length = length;
        _layout = layout;
        Source = source;
        _rootPaths = new string[layout.RootCount];
        for (var r = 0; r < _rootPaths.Length; r++)
        {
            _rootPaths[r] = Encoding.UTF8.GetString(RootPathBytes(r));
        }
    }

    public string Source { get; }

    public int EntryCount => _layout.EntryCount;

    public int RootCount => _layout.RootCount;

    public int SegmentCount => _layout.SegmentCount;

    public ReadOnlySpan<byte> Image => new(_base, _length);

    public ReadOnlySpan<uint> Parents => MemoryMarshal.Cast<byte, uint>(Image[_layout.Parents]);

    public ReadOnlySpan<uint> Names => MemoryMarshal.Cast<byte, uint>(Image[_layout.Names]);

    public ReadOnlySpan<byte> Flags => Image[_layout.Flags];

    public ReadOnlySpan<uint> SegmentOffsets => MemoryMarshal.Cast<byte, uint>(Image[_layout.SegmentOffsets]);

    // every segment followed by its NUL
    public ReadOnlySpan<byte> SegmentBlob => Image[_layout.SegmentBlob];

    public ReadOnlySpan<uint> NonAsciiSegments => MemoryMarshal.Cast<byte, uint>(Image[_layout.NonAscii]);

    public string RootPath(int root) => _rootPaths[root];

    // utf-8 bytes of one segment, without its NUL
    public ReadOnlySpan<byte> Segment(uint segment)
    {
        var offsets = SegmentOffsets;
        var start = (int)offsets[(int)segment];
        return SegmentBlob[start..((int)offsets[(int)segment + 1] - 1)];
    }

    public bool IsDirectory(int entry) => (Flags[entry] & SnapshotFormat.FLAG_DIRECTORY) != 0;

    public SnapshotInfo Info()
    {
        var roots = new SnapshotRoot[RootCount];
        var records = Image[_layout.Roots];
        for (var r = 0; r < roots.Length; r++)
        {
            var record = records[(r * SnapshotFormat.ROOT_RECORD_SIZE)..];
            roots[r] = new SnapshotRoot(_rootPaths[r], (int)BinaryPrimitives.ReadUInt32LittleEndian(record[12..]),
                TimeSpan.FromTicks(BinaryPrimitives.ReadInt64LittleEndian(record[16..])));
        }

        return new SnapshotInfo(Source, DateTimeOffset.FromUnixTimeMilliseconds(_layout.BuiltAtUnixMs), EntryCount, SegmentCount, _length, roots);
    }

    // null when there's no file. any other failure is IndexUnreadableException
    public static Snapshot? Open(string path)
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
            throw new IndexUnreadableException(path, e.Message);
        }

        MemoryMappedFile? file = null;
        MemoryMappedViewAccessor? view = null;
        var acquired = false;
        try
        {
            var length = stream.Length;
            if (length < SnapshotFormat.HEADER_SIZE || length > int.MaxValue)
            {
                throw new IndexUnreadableException(path, length < SnapshotFormat.HEADER_SIZE ? "truncated header" : "too large");
            }

            file = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
            view = file.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
            byte* start = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref start);
            acquired = true;
            start += view.PointerOffset;
            var reason = SnapshotChecks.Validate(new ReadOnlySpan<byte>(start, (int)length), out var layout);
            if (reason is not null)
            {
                throw new IndexUnreadableException(path, reason);
            }

            return new Snapshot(file, view, default, start, (int)length, layout, path);
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
            if (e is IndexUnreadableException)
            {
                throw;
            }

            if (e is IOException or UnauthorizedAccessException)
            {
                throw new IndexUnreadableException(path, e.Message);
            }
            throw;
        }
    }

    // tests and benches skip the disk. the array stays pinned until Dispose
    public static Snapshot FromBytes(byte[] image, string source = "memory")
    {
        var reason = SnapshotChecks.Validate(image, out var layout);
        if (reason is not null)
        {
            throw new IndexUnreadableException(source, reason);
        }

        return FromValidatedBytes(image, layout, source);
    }

    // a fuzz test skips the checksum to reach the structural checks with arbitrary bytes behind them
    internal static Snapshot? FromBytesSkippingChecksum(byte[] image)
    {
        if (image.Length < SnapshotFormat.HEADER_SIZE || SnapshotChecks.ValidateStructure(image, out var layout) is not null)
        {
            return null;
        }

        return FromValidatedBytes(image, layout, "memory");
    }

    static Snapshot FromValidatedBytes(byte[] image, SnapshotLayout layout, string source)
    {
        var pinned = GCHandle.Alloc(image, GCHandleType.Pinned);
        return new Snapshot(null, null, pinned, (byte*)pinned.AddrOfPinnedObject(), image.Length, layout, source);
    }

    ReadOnlySpan<byte> RootPathBytes(int root)
    {
        var record = Image[_layout.Roots][(root * SnapshotFormat.ROOT_RECORD_SIZE)..];
        var start = (int)BinaryPrimitives.ReadUInt32LittleEndian(record[4..]);
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(record[8..]);
        return Image[_layout.RootPaths].Slice(start, length);
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
}
