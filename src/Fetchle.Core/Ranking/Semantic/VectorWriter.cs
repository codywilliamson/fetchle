using System.Buffers.Binary;
using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace Fetchle.Core.Ranking.Semantic;

// lays VectorParts out as one vectors.fvec image, checksummed, written once
static class VectorWriter
{
    public static byte[] ToBytes(VectorParts parts, ulong snapshotChecksum, ulong modelChecksum)
    {
        var segments = parts.Codes.Length / SignCodes.BYTES;
        var lengths = new long[VectorFormat.SECTION_COUNT];
        lengths[VectorFormat.CODES] = parts.Codes.Length;
        lengths[VectorFormat.EMPTY] = (long)parts.Empty.Length * sizeof(ulong);
        lengths[VectorFormat.NORMS] = (long)parts.Norms.Length * sizeof(float);

        var starts = new long[VectorFormat.SECTION_COUNT];
        long end = VectorFormat.HEADER_SIZE;
        for (var s = 0; s < starts.Length; s++)
        {
            starts[s] = VectorFormat.Align(end);
            end = starts[s] + lengths[s];
        }

        var image = new byte[VectorFormat.Align(end)];
        parts.Codes.CopyTo(image.AsSpan((int)starts[VectorFormat.CODES]));
        MemoryMarshal.AsBytes(parts.Empty.AsSpan()).CopyTo(image.AsSpan((int)starts[VectorFormat.EMPTY]));
        MemoryMarshal.AsBytes(parts.Norms.AsSpan()).CopyTo(image.AsSpan((int)starts[VectorFormat.NORMS]));

        var header = image.AsSpan();
        BinaryPrimitives.WriteUInt64LittleEndian(header[VectorFormat.MAGIC_OFFSET..], VectorFormat.MAGIC);
        BinaryPrimitives.WriteUInt32LittleEndian(header[VectorFormat.VERSION_OFFSET..], VectorFormat.VERSION);
        // native order on purpose: a reader on the other endianness sees 0x04030201 and refuses the file
        MemoryMarshal.Write(header[VectorFormat.ENDIAN_OFFSET..], VectorFormat.ENDIAN_MARKER);
        BinaryPrimitives.WriteUInt64LittleEndian(header[VectorFormat.LENGTH_OFFSET..], (ulong)image.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(header[VectorFormat.SNAPSHOT_CHECKSUM_OFFSET..], snapshotChecksum);
        BinaryPrimitives.WriteUInt64LittleEndian(header[VectorFormat.MODEL_CHECKSUM_OFFSET..], modelChecksum);
        BinaryPrimitives.WriteUInt32LittleEndian(header[VectorFormat.SEGMENT_COUNT_OFFSET..], (uint)segments);
        BinaryPrimitives.WriteUInt32LittleEndian(header[VectorFormat.ENTRY_COUNT_OFFSET..], (uint)parts.Norms.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(header[VectorFormat.CODE_BYTES_OFFSET..], SignCodes.BYTES);
        BinaryPrimitives.WriteUInt32LittleEndian(header[VectorFormat.SECTION_COUNT_OFFSET..], VectorFormat.SECTION_COUNT);
        for (var s = 0; s < starts.Length; s++)
        {
            var entry = header[(VectorFormat.SECTION_TABLE_OFFSET + s * VectorFormat.SECTION_ENTRY_SIZE)..];
            BinaryPrimitives.WriteUInt64LittleEndian(entry, (ulong)starts[s]);
            BinaryPrimitives.WriteUInt64LittleEndian(entry[sizeof(ulong)..], (ulong)lengths[s]);
        }

        BinaryPrimitives.WriteUInt64LittleEndian(header[VectorFormat.CHECKSUM_OFFSET..], Checksum(image));
        return image;
    }

    public static ulong Checksum(ReadOnlySpan<byte> image) => Index.ChunkedChecksum.Compute(image[VectorFormat.CHECKSUMMED_FROM..]);
}
