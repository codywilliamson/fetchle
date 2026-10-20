using System.Buffers.Binary;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;

namespace Fetchle.Core.Index;

// lays a PathTree out as one snapshot image. the whole image is built in memory, checksummed, then written once
static class SnapshotWriter
{
    public static byte[] ToBytes(PathTree tree, DateTimeOffset builtAt)
    {
        var (offsets, blob, nonAscii) = EncodeSegments(tree.Segments);
        var (rootRecords, rootPaths) = EncodeRoots(tree.Roots);

        var lengths = new long[SnapshotFormat.SECTION_COUNT];
        lengths[SnapshotFormat.PARENTS] = (long)tree.EntryCount * sizeof(uint);
        lengths[SnapshotFormat.NAMES] = (long)tree.EntryCount * sizeof(uint);
        lengths[SnapshotFormat.FLAGS] = tree.EntryCount;
        lengths[SnapshotFormat.SEGMENT_OFFSETS] = (long)offsets.Length * sizeof(uint);
        lengths[SnapshotFormat.SEGMENT_BLOB] = blob.Length;
        lengths[SnapshotFormat.NON_ASCII] = (long)nonAscii.Count * sizeof(uint);
        lengths[SnapshotFormat.ROOTS] = rootRecords.Length;
        lengths[SnapshotFormat.ROOT_PATHS] = rootPaths.Length;

        var starts = new long[SnapshotFormat.SECTION_COUNT];
        long end = SnapshotFormat.HEADER_SIZE;
        for (var s = 0; s < starts.Length; s++)
        {
            starts[s] = SnapshotFormat.Align(end);
            end = starts[s] + lengths[s];
        }

        var image = new byte[SnapshotFormat.Align(end)];
        Copy(image, starts[SnapshotFormat.PARENTS], MemoryMarshal.AsBytes(tree.Parents.AsSpan()));
        Copy(image, starts[SnapshotFormat.NAMES], MemoryMarshal.AsBytes(tree.Names.AsSpan()));
        Copy(image, starts[SnapshotFormat.FLAGS], tree.Flags);
        Copy(image, starts[SnapshotFormat.SEGMENT_OFFSETS], MemoryMarshal.AsBytes(offsets.AsSpan()));
        Copy(image, starts[SnapshotFormat.SEGMENT_BLOB], blob);
        Copy(image, starts[SnapshotFormat.NON_ASCII], MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(nonAscii)));
        Copy(image, starts[SnapshotFormat.ROOTS], rootRecords);
        Copy(image, starts[SnapshotFormat.ROOT_PATHS], rootPaths);

        var header = image.AsSpan();
        BinaryPrimitives.WriteUInt64LittleEndian(header[SnapshotFormat.MAGIC_OFFSET..], SnapshotFormat.MAGIC);
        BinaryPrimitives.WriteUInt32LittleEndian(header[SnapshotFormat.VERSION_OFFSET..], SnapshotFormat.VERSION);
        // native order on purpose: a reader on the other endianness sees 0x04030201 and refuses the file
        MemoryMarshal.Write(header[SnapshotFormat.ENDIAN_OFFSET..], SnapshotFormat.ENDIAN_MARKER);
        BinaryPrimitives.WriteUInt64LittleEndian(header[SnapshotFormat.LENGTH_OFFSET..], (ulong)image.Length);
        BinaryPrimitives.WriteInt64LittleEndian(header[SnapshotFormat.BUILT_AT_OFFSET..], builtAt.ToUnixTimeMilliseconds());
        BinaryPrimitives.WriteUInt32LittleEndian(header[SnapshotFormat.ENTRY_COUNT_OFFSET..], (uint)tree.EntryCount);
        BinaryPrimitives.WriteUInt32LittleEndian(header[SnapshotFormat.SEGMENT_COUNT_OFFSET..], (uint)tree.Segments.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(header[SnapshotFormat.ROOT_COUNT_OFFSET..], (uint)tree.Roots.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(header[SnapshotFormat.NON_ASCII_COUNT_OFFSET..], (uint)nonAscii.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(header[SnapshotFormat.SECTION_COUNT_OFFSET..], SnapshotFormat.SECTION_COUNT);
        for (var s = 0; s < starts.Length; s++)
        {
            var entry = header[(SnapshotFormat.SECTION_TABLE_OFFSET + s * SnapshotFormat.SECTION_ENTRY_SIZE)..];
            BinaryPrimitives.WriteUInt64LittleEndian(entry, (ulong)starts[s]);
            BinaryPrimitives.WriteUInt64LittleEndian(entry[sizeof(ulong)..], (ulong)lengths[s]);
        }

        BinaryPrimitives.WriteUInt64LittleEndian(header[SnapshotFormat.CHECKSUM_OFFSET..], Checksum(image));
        return image;
    }

    public static ulong Checksum(ReadOnlySpan<byte> image) => XxHash3.HashToUInt64(image[SnapshotFormat.CHECKSUMMED_FROM..]);

    static (uint[] Offsets, byte[] Blob, List<uint> NonAscii) EncodeSegments(List<string> segments)
    {
        var offsets = new uint[segments.Count + 1];
        long size = 0;
        foreach (var segment in segments)
        {
            size += Encoding.UTF8.GetByteCount(segment) + 1;
        }

        if (size > uint.MaxValue)
        {
            throw new InvalidOperationException($"name segments need {size} bytes, the format holds at most {uint.MaxValue}");
        }

        var blob = new byte[size];
        var nonAscii = new List<uint>();
        var at = 0;
        for (var i = 0; i < segments.Count; i++)
        {
            offsets[i] = (uint)at;
            var written = Encoding.UTF8.GetBytes(segments[i], blob.AsSpan(at));
            if (!System.Text.Ascii.IsValid(blob.AsSpan(at, written)))
            {
                nonAscii.Add((uint)i);
            }

            // the terminator is already 0, it keeps a match from running across two segments
            at += written + 1;
        }

        offsets[segments.Count] = (uint)at;
        return (offsets, blob, nonAscii);
    }

    static (byte[] Records, byte[] Paths) EncodeRoots(List<TreeRoot> roots)
    {
        var records = new byte[roots.Count * SnapshotFormat.ROOT_RECORD_SIZE];
        var paths = new List<byte>();
        for (var r = 0; r < roots.Count; r++)
        {
            var path = Encoding.UTF8.GetBytes(roots[r].Path);
            var record = records.AsSpan(r * SnapshotFormat.ROOT_RECORD_SIZE);
            BinaryPrimitives.WriteUInt32LittleEndian(record, (uint)r);
            BinaryPrimitives.WriteUInt32LittleEndian(record[4..], (uint)paths.Count);
            BinaryPrimitives.WriteUInt32LittleEndian(record[8..], (uint)path.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(record[12..], (uint)roots[r].Entries);
            BinaryPrimitives.WriteInt64LittleEndian(record[16..], roots[r].WalkTime.Ticks);
            paths.AddRange(path);
        }

        return (records, [.. paths]);
    }

    static void Copy(byte[] image, long start, ReadOnlySpan<byte> bytes) => bytes.CopyTo(image.AsSpan((int)start));
}
