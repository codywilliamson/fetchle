using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

namespace Fetchle.Core.Index;

// where each section sits in a snapshot image, read from a header that Validate already checked
readonly record struct SnapshotLayout(
    long BuiltAtUnixMs,
    int EntryCount,
    int SegmentCount,
    int RootCount,
    Range Parents,
    Range Names,
    Range Flags,
    Range SegmentOffsets,
    Range SegmentBlob,
    Range NonAscii,
    Range Roots,
    Range RootPaths);

// every check a reader needs before it can index into the image without surprises. returns why the image
// can't be trusted, or null. linear and mostly vectorized, so it runs on every open
static class SnapshotChecks
{
    static readonly long MAX_UNIX_MS = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

    public static string? Validate(ReadOnlySpan<byte> image, out SnapshotLayout layout)
    {
        layout = default;
        if (image.Length < SnapshotFormat.HEADER_SIZE)
        {
            return "truncated header";
        }

        if (BinaryPrimitives.ReadUInt64LittleEndian(image[SnapshotFormat.MAGIC_OFFSET..]) != SnapshotFormat.MAGIC)
        {
            return "not a fetchle snapshot";
        }

        if (MemoryMarshal.Read<uint>(image[SnapshotFormat.ENDIAN_OFFSET..]) != SnapshotFormat.ENDIAN_MARKER)
        {
            return "wrong endianness";
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(image[SnapshotFormat.VERSION_OFFSET..]);
        if (version != SnapshotFormat.VERSION)
        {
            return $"version {version}, this fetchle reads {SnapshotFormat.VERSION}";
        }

        if (BinaryPrimitives.ReadUInt64LittleEndian(image[SnapshotFormat.LENGTH_OFFSET..]) != (ulong)image.Length)
        {
            return "length mismatch, truncated or extended";
        }

        if (BinaryPrimitives.ReadUInt64LittleEndian(image[SnapshotFormat.CHECKSUM_OFFSET..]) != SnapshotWriter.Checksum(image))
        {
            return "checksum mismatch";
        }

        return ValidateStructure(image, out layout);
    }

    // past the checksum the bytes are whatever some writer wrote; this still refuses anything a reader could trip on
    internal static string? ValidateStructure(ReadOnlySpan<byte> image, out SnapshotLayout layout)
    {
        layout = default;
        var entries = BinaryPrimitives.ReadUInt32LittleEndian(image[SnapshotFormat.ENTRY_COUNT_OFFSET..]);
        var segments = BinaryPrimitives.ReadUInt32LittleEndian(image[SnapshotFormat.SEGMENT_COUNT_OFFSET..]);
        var roots = BinaryPrimitives.ReadUInt32LittleEndian(image[SnapshotFormat.ROOT_COUNT_OFFSET..]);
        var nonAscii = BinaryPrimitives.ReadUInt32LittleEndian(image[SnapshotFormat.NON_ASCII_COUNT_OFFSET..]);
        if (BinaryPrimitives.ReadUInt32LittleEndian(image[SnapshotFormat.SECTION_COUNT_OFFSET..]) != SnapshotFormat.SECTION_COUNT)
        {
            return "unexpected section count";
        }

        var builtAt = BinaryPrimitives.ReadInt64LittleEndian(image[SnapshotFormat.BUILT_AT_OFFSET..]);
        if (builtAt < 0 || builtAt > MAX_UNIX_MS)
        {
            return "build time out of range";
        }

        if (entries > int.MaxValue / sizeof(uint) || segments >= int.MaxValue / sizeof(uint) || nonAscii > segments || roots > entries)
        {
            return "counts out of range";
        }

        const long ANY = -1;
        Span<long> expected =
        [
            entries * (long)sizeof(uint),
            entries * (long)sizeof(uint),
            entries,
            (segments + 1L) * sizeof(uint),
            ANY,
            nonAscii * (long)sizeof(uint),
            roots * (long)SnapshotFormat.ROOT_RECORD_SIZE,
            ANY,
        ];

        Span<Range> ranges = stackalloc Range[SnapshotFormat.SECTION_COUNT];
        for (var s = 0; s < ranges.Length; s++)
        {
            var entry = image[(SnapshotFormat.SECTION_TABLE_OFFSET + s * SnapshotFormat.SECTION_ENTRY_SIZE)..];
            var start = BinaryPrimitives.ReadUInt64LittleEndian(entry);
            var length = BinaryPrimitives.ReadUInt64LittleEndian(entry[sizeof(ulong)..]);
            if (start < SnapshotFormat.HEADER_SIZE || start % SnapshotFormat.SECTION_ALIGNMENT != 0
                || start > (ulong)image.Length || length > (ulong)image.Length - start)
            {
                return $"section {s} out of bounds";
            }

            if (expected[s] != ANY && (ulong)expected[s] != length)
            {
                return $"section {s} has the wrong size";
            }

            ranges[s] = new Range((int)start, (int)(start + length));
        }

        layout = new SnapshotLayout(
            builtAt,
            (int)entries, (int)segments, (int)roots,
            ranges[SnapshotFormat.PARENTS], ranges[SnapshotFormat.NAMES], ranges[SnapshotFormat.FLAGS],
            ranges[SnapshotFormat.SEGMENT_OFFSETS], ranges[SnapshotFormat.SEGMENT_BLOB], ranges[SnapshotFormat.NON_ASCII],
            ranges[SnapshotFormat.ROOTS], ranges[SnapshotFormat.ROOT_PATHS]);

        return ValidateSegments(image, layout) ?? ValidateEntries(image, layout) ?? ValidateRoots(image, layout);
    }

    static string? ValidateSegments(ReadOnlySpan<byte> image, SnapshotLayout layout)
    {
        var offsets = MemoryMarshal.Cast<byte, uint>(image[layout.SegmentOffsets]);
        var blob = image[layout.SegmentBlob];
        if (offsets[0] != 0 || offsets[^1] != (uint)blob.Length)
        {
            return "segment offsets don't span the blob";
        }

        // every segment is at least one byte plus its NUL, and NULs appear only as terminators
        for (var i = 0; i < layout.SegmentCount; i++)
        {
            if ((long)offsets[i + 1] < offsets[i] + 2L || offsets[i + 1] > (uint)blob.Length || blob[(int)offsets[i + 1] - 1] != 0)
            {
                return "segment offsets out of order";
            }
        }

        if (blob.Count((byte)0) != layout.SegmentCount)
        {
            return "segment blob has stray NULs";
        }

        if (!Utf8.IsValid(blob))
        {
            return "segment blob isn't utf-8";
        }

        var nonAscii = MemoryMarshal.Cast<byte, uint>(image[layout.NonAscii]);
        if (nonAscii.IsEmpty)
        {
            return Ascii.IsValid(blob) ? null : "non-ascii segments not listed";
        }

        for (var i = 0; i < nonAscii.Length; i++)
        {
            if (nonAscii[i] >= (uint)layout.SegmentCount || (i > 0 && nonAscii[i] <= nonAscii[i - 1]))
            {
                return "non-ascii ids out of order";
            }
        }
        return null;
    }

    static string? ValidateEntries(ReadOnlySpan<byte> image, SnapshotLayout layout)
    {
        var parents = MemoryMarshal.Cast<byte, uint>(image[layout.Parents]);
        var names = MemoryMarshal.Cast<byte, uint>(image[layout.Names]);
        var flags = image[layout.Flags];
        for (var i = 0; i < layout.RootCount; i++)
        {
            if (parents[i] != SnapshotFormat.NO_PARENT || names[i] != (uint)i || flags[i] != (SnapshotFormat.FLAG_DIRECTORY | SnapshotFormat.FLAG_ROOT))
            {
                return "bad root entry";
            }
        }

        var previous = 0u;
        var segments = (uint)layout.SegmentCount;
        for (var i = layout.RootCount; i < parents.Length; i++)
        {
            var parent = parents[i];
            // parent before child, parents never decrease, and only dirs have children
            if (parent >= (uint)i || parent < previous || names[i] >= segments
                || (flags[i] & ~SnapshotFormat.FLAG_DIRECTORY) != 0 || (flags[(int)parent] & SnapshotFormat.FLAG_DIRECTORY) == 0)
            {
                return "bad entry";
            }

            previous = parent;
        }
        return null;
    }

    static string? ValidateRoots(ReadOnlySpan<byte> image, SnapshotLayout layout)
    {
        var records = image[layout.Roots];
        var paths = image[layout.RootPaths];
        long entries = layout.RootCount;
        for (var r = 0; r < layout.RootCount; r++)
        {
            var record = records[(r * SnapshotFormat.ROOT_RECORD_SIZE)..];
            var start = BinaryPrimitives.ReadUInt32LittleEndian(record[4..]);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(record[8..]);
            if (BinaryPrimitives.ReadUInt32LittleEndian(record) != (uint)r || length == 0
                || start > (uint)paths.Length || length > (uint)paths.Length - start
                || !Utf8.IsValid(paths.Slice((int)start, (int)length))
                || BinaryPrimitives.ReadUInt32LittleEndian(record[SnapshotFormat.ROOT_FILES_OFFSET..]) > BinaryPrimitives.ReadUInt32LittleEndian(record[SnapshotFormat.ROOT_ENTRIES_OFFSET..])
                || BinaryPrimitives.ReadInt64LittleEndian(record[SnapshotFormat.ROOT_WALK_TICKS_OFFSET..]) < 0)
            {
                return "bad root record";
            }

            entries += BinaryPrimitives.ReadUInt32LittleEndian(record[SnapshotFormat.ROOT_ENTRIES_OFFSET..]);
        }

        return entries == layout.EntryCount ? null : "root entry counts don't add up";
    }
}
