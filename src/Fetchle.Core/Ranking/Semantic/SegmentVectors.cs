using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Fetchle.Core.Index;

namespace Fetchle.Core.Ranking.Semantic;

// a validated, memory-mapped vectors.fvec, read in place like the snapshot. vectors are derived data: a missing,
// stale or broken file never fails a search, it only turns the semantic retriever off, and Problem says why
sealed unsafe class SegmentVectors : IDisposable
{
    readonly MemoryMappedFile? _file;
    readonly MemoryMappedViewAccessor? _view;
    GCHandle _pinned;
    readonly byte* _base;
    readonly int _length;
    readonly Range _codes;
    readonly Range _empty;
    readonly Range _norms;
    bool _disposed;

    SegmentVectors(MemoryMappedFile? file, MemoryMappedViewAccessor? view, GCHandle pinned, byte* start, int length, Range codes, Range empty, Range norms)
    {
        _file = file;
        _view = view;
        _pinned = pinned;
        _base = start;
        _length = length;
        _codes = codes;
        _empty = empty;
        _norms = norms;
    }

    ReadOnlySpan<byte> Image => new(_base, _length);

    public ReadOnlySpan<byte> Codes => Image[_codes];

    public ReadOnlySpan<ulong> Empty => MemoryMarshal.Cast<byte, ulong>(Image[_empty]);

    public ReadOnlySpan<float> Norms => MemoryMarshal.Cast<byte, float>(Image[_norms]);

    public ulong ModelChecksum => BinaryPrimitives.ReadUInt64LittleEndian(Image[VectorFormat.MODEL_CHECKSUM_OFFSET..]);

    public ReadOnlySpan<byte> Code(uint segment) => Codes.Slice((int)segment * SignCodes.BYTES, SignCodes.BYTES);

    // null with a reason when there's no usable file for this snapshot and model
    public static SegmentVectors? Open(string path, Snapshot snapshot, ulong modelChecksum, out string? problem)
    {
        problem = null;
        if (!File.Exists(path))
        {
            problem = "no vectors yet";
            return null;
        }

        FileStream? stream = null;
        MemoryMappedFile? file = null;
        MemoryMappedViewAccessor? view = null;
        var acquired = false;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, bufferSize: 1);
            var length = stream.Length;
            if (length < VectorFormat.HEADER_SIZE || length > int.MaxValue)
            {
                stream.Dispose();
                problem = "vectors file truncated or too large";
                return null;
            }

            // the mapping owns the stream from here and closes it
            file = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
            view = file.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
            byte* start = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref start);
            acquired = true;
            start += view.PointerOffset;
            problem = Validate(new ReadOnlySpan<byte>(start, (int)length), snapshot, modelChecksum, out var codes, out var empty, out var norms);
            if (problem is not null)
            {
                Release(view, file);
                return null;
            }
            return new SegmentVectors(file, view, default, start, (int)length, codes, empty, norms);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (acquired)
            {
                Release(view!, file!);
            }
            else
            {
                view?.Dispose();
                file?.Dispose();
                stream?.Dispose();
            }
            problem = $"vectors unreadable: {e.Message}";
            return null;
        }
    }

    // evals, tests and live walks keep vectors in memory
    public static SegmentVectors FromBytes(byte[] image, Snapshot snapshot, ulong modelChecksum)
    {
        var problem = Validate(image, snapshot, modelChecksum, out var codes, out var empty, out var norms);
        if (problem is not null)
        {
            throw new InvalidDataException(problem);
        }

        var pinned = GCHandle.Alloc(image, GCHandleType.Pinned);
        return new SegmentVectors(null, null, pinned, (byte*)pinned.AddrOfPinnedObject(), image.Length, codes, empty, norms);
    }

    static string? Validate(ReadOnlySpan<byte> image, Snapshot snapshot, ulong modelChecksum, out Range codes, out Range empty, out Range norms)
    {
        codes = empty = norms = default;
        if (image.Length < VectorFormat.HEADER_SIZE || BinaryPrimitives.ReadUInt64LittleEndian(image) != VectorFormat.MAGIC)
        {
            return "not a fetchle vectors file";
        }

        if (MemoryMarshal.Read<uint>(image[VectorFormat.ENDIAN_OFFSET..]) != VectorFormat.ENDIAN_MARKER
            || BinaryPrimitives.ReadUInt32LittleEndian(image[VectorFormat.VERSION_OFFSET..]) != VectorFormat.VERSION)
        {
            return "vectors file from another version or endianness";
        }

        if (BinaryPrimitives.ReadUInt64LittleEndian(image[VectorFormat.LENGTH_OFFSET..]) != (ulong)image.Length
            || BinaryPrimitives.ReadUInt64LittleEndian(image[VectorFormat.CHECKSUM_OFFSET..]) != VectorWriter.Checksum(image))
        {
            return "vectors file corrupt";
        }

        // a rebuild swaps the snapshot before its vectors are ready, so a mismatch is normal for a few seconds
        if (BinaryPrimitives.ReadUInt64LittleEndian(image[VectorFormat.SNAPSHOT_CHECKSUM_OFFSET..]) != snapshot.Checksum)
        {
            return "vectors are for another snapshot";
        }

        if (BinaryPrimitives.ReadUInt64LittleEndian(image[VectorFormat.MODEL_CHECKSUM_OFFSET..]) != modelChecksum)
        {
            return "vectors are from another model";
        }

        var segments = BinaryPrimitives.ReadUInt32LittleEndian(image[VectorFormat.SEGMENT_COUNT_OFFSET..]);
        var entries = BinaryPrimitives.ReadUInt32LittleEndian(image[VectorFormat.ENTRY_COUNT_OFFSET..]);
        if (segments != (uint)snapshot.SegmentCount || entries != (uint)snapshot.EntryCount
            || BinaryPrimitives.ReadUInt32LittleEndian(image[VectorFormat.CODE_BYTES_OFFSET..]) != SignCodes.BYTES
            || BinaryPrimitives.ReadUInt32LittleEndian(image[VectorFormat.SECTION_COUNT_OFFSET..]) != VectorFormat.SECTION_COUNT)
        {
            return "vectors file shape doesn't match the snapshot";
        }

        Span<long> expected = [(long)segments * SignCodes.BYTES, (long)VectorFormat.EmptyWords((int)segments) * sizeof(ulong), (long)entries * sizeof(float)];
        Span<Range> ranges = stackalloc Range[VectorFormat.SECTION_COUNT];
        for (var s = 0; s < ranges.Length; s++)
        {
            var entry = image[(VectorFormat.SECTION_TABLE_OFFSET + s * VectorFormat.SECTION_ENTRY_SIZE)..];
            var start = BinaryPrimitives.ReadUInt64LittleEndian(entry);
            var length = BinaryPrimitives.ReadUInt64LittleEndian(entry[sizeof(ulong)..]);
            if (start < VectorFormat.HEADER_SIZE || start % VectorFormat.SECTION_ALIGNMENT != 0 || start > (ulong)image.Length
                || length > (ulong)image.Length - start || length != (ulong)expected[s])
            {
                return $"vectors section {s} out of bounds";
            }
            ranges[s] = new Range((int)start, (int)(start + length));
        }

        // a NaN or negative length would poison every score it divides
        foreach (var norm in MemoryMarshal.Cast<byte, float>(image[ranges[VectorFormat.NORMS]]))
        {
            if (!float.IsFinite(norm) || norm < 0)
            {
                return "vectors file has a bad norm";
            }
        }

        codes = ranges[VectorFormat.CODES];
        empty = ranges[VectorFormat.EMPTY];
        norms = ranges[VectorFormat.NORMS];
        return null;
    }

    static void Release(MemoryMappedViewAccessor view, MemoryMappedFile file)
    {
        view.SafeMemoryMappedViewHandle.ReleasePointer();
        view.Dispose();
        file.Dispose();
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
            Release(_view, _file!);
        }

        if (_pinned.IsAllocated)
        {
            _pinned.Free();
        }
    }
}
