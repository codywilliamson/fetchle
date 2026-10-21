using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.IO.Hashing;

namespace Fetchle.Core.Embedding;

// where each section sits in a model image, read from a header that Validate already checked
readonly record struct ModelLayout(
    int Dims,
    int Vocab,
    int UnkId,
    int MaxWordChars,
    float Scale,
    int MaxTokenBytes,
    byte[] Prefix,
    Range TokenOffsets,
    Range TokenBlob,
    Range Lookup,
    Range Embeddings,
    Range Mean);

// every check a reader needs before it can index into the image without surprises. returns why the image
// can't be trusted, or null. the checksum is one pass over the file; the structure checks walk the token
// offsets and the lookup order once, which is a few ms at 63k tokens
static class ModelChecks
{
    public static string? Validate(ReadOnlySpan<byte> image, out ModelLayout layout)
    {
        layout = default;
        if (image.Length < ModelFormat.HEADER_SIZE)
        {
            return "truncated header";
        }

        if (BinaryPrimitives.ReadUInt64LittleEndian(image[ModelFormat.MAGIC_OFFSET..]) != ModelFormat.MAGIC)
        {
            return "not a fetchle model";
        }

        if (MemoryMarshal.Read<uint>(image[ModelFormat.ENDIAN_OFFSET..]) != ModelFormat.ENDIAN_MARKER)
        {
            return "wrong endianness";
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(image[ModelFormat.VERSION_OFFSET..]);
        if (version != ModelFormat.VERSION)
        {
            return $"version {version}, this fetchle reads {ModelFormat.VERSION}";
        }

        if (BinaryPrimitives.ReadUInt64LittleEndian(image[ModelFormat.LENGTH_OFFSET..]) != (ulong)image.Length)
        {
            return "length mismatch, truncated or extended";
        }

        if (BinaryPrimitives.ReadUInt64LittleEndian(image[ModelFormat.CHECKSUM_OFFSET..]) != Checksum(image))
        {
            return "checksum mismatch";
        }

        return ValidateStructure(image, out layout);
    }

    public static ulong Checksum(ReadOnlySpan<byte> image) => XxHash3.HashToUInt64(image[ModelFormat.CHECKSUMMED_FROM..]);

    // past the checksum the bytes are whatever some writer wrote; this still refuses anything a reader could trip on
    internal static string? ValidateStructure(ReadOnlySpan<byte> image, out ModelLayout layout)
    {
        layout = default;
        var dims = BinaryPrimitives.ReadUInt32LittleEndian(image[ModelFormat.DIMS_OFFSET..]);
        var vocab = BinaryPrimitives.ReadUInt32LittleEndian(image[ModelFormat.VOCAB_OFFSET..]);
        var unk = BinaryPrimitives.ReadUInt32LittleEndian(image[ModelFormat.UNK_OFFSET..]);
        var maxWordChars = BinaryPrimitives.ReadUInt32LittleEndian(image[ModelFormat.MAX_WORD_CHARS_OFFSET..]);
        var scale = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(image[ModelFormat.SCALE_OFFSET..]));
        var prefixLength = BinaryPrimitives.ReadUInt32LittleEndian(image[ModelFormat.PREFIX_LENGTH_OFFSET..]);

        if (dims is 0 or > ModelFormat.MAX_DIMS)
        {
            return "dims out of range";
        }

        if (vocab == 0 || vocab > int.MaxValue / 2 || unk >= vocab)
        {
            return "vocab out of range";
        }

        if (maxWordChars == 0 || maxWordChars > 1024)
        {
            return "max word chars out of range";
        }

        if (!float.IsFinite(scale) || scale <= 0)
        {
            return "bad scale";
        }

        if (prefixLength > ModelFormat.MAX_PREFIX_BYTES)
        {
            return "prefix too long";
        }

        var expected = new ulong[]
        {
            (vocab + 1UL) * 4,
            0, // the blob's length comes from its last offset
            vocab * 4UL,
            vocab * (ulong)dims,
            dims * 4UL,
        };

        var sections = new Range[ModelFormat.SECTION_COUNT];
        var next = (ulong)ModelFormat.HEADER_SIZE;
        for (var s = 0; s < sections.Length; s++)
        {
            var entry = image[(ModelFormat.SECTION_TABLE_OFFSET + s * ModelFormat.SECTION_ENTRY_SIZE)..];
            var offset = BinaryPrimitives.ReadUInt64LittleEndian(entry);
            var length = BinaryPrimitives.ReadUInt64LittleEndian(entry[8..]);
            if (offset % ModelFormat.SECTION_ALIGNMENT != 0 || offset < next)
            {
                return $"section {s} misplaced";
            }

            if (offset > (ulong)image.Length || length > (ulong)image.Length - offset)
            {
                return $"section {s} out of bounds";
            }

            if (s != ModelFormat.TOKEN_BLOB && length != expected[s])
            {
                return $"section {s} has the wrong size";
            }

            sections[s] = new Range((int)offset, (int)(offset + length));
            next = offset + length;
        }

        var offsets = MemoryMarshal.Cast<byte, uint>(image[sections[ModelFormat.TOKEN_OFFSETS]]);
        var blob = image[sections[ModelFormat.TOKEN_BLOB]];
        var maxTokenBytes = CheckTokenOffsets(offsets, blob.Length);
        if (maxTokenBytes < 0)
        {
            return "token offsets are not ascending within the blob";
        }

        var lookup = MemoryMarshal.Cast<byte, uint>(image[sections[ModelFormat.LOOKUP]]);
        if (!LookupIsStrictlyAscending(lookup, offsets, blob))
        {
            return "lookup is not a strictly ascending permutation of the tokens";
        }

        var mean = MemoryMarshal.Cast<byte, float>(image[sections[ModelFormat.MEAN]]);
        foreach (var value in mean)
        {
            if (!float.IsFinite(value))
            {
                return "mean is not finite";
            }
        }

        var prefix = image.Slice(ModelFormat.PREFIX_OFFSET, (int)prefixLength).ToArray();
        layout = new ModelLayout((int)dims, (int)vocab, (int)unk, (int)maxWordChars, scale, maxTokenBytes, prefix,
            sections[ModelFormat.TOKEN_OFFSETS], sections[ModelFormat.TOKEN_BLOB], sections[ModelFormat.LOOKUP],
            sections[ModelFormat.EMBEDDINGS], sections[ModelFormat.MEAN]);
        return null;
    }

    // starts at 0, never goes down, ends exactly at the blob's end. returns the longest token or -1
    static int CheckTokenOffsets(ReadOnlySpan<uint> offsets, int blobLength)
    {
        if (offsets[0] != 0 || offsets[^1] != (uint)blobLength)
        {
            return -1;
        }

        uint longest = 0;
        for (var i = 1; i < offsets.Length; i++)
        {
            if (offsets[i] < offsets[i - 1])
            {
                return -1;
            }

            longest = Math.Max(longest, offsets[i] - offsets[i - 1]);
        }

        return (int)longest;
    }

    // ids in range and strictly ascending bytes: no repeated token, so every id appears once
    static bool LookupIsStrictlyAscending(ReadOnlySpan<uint> lookup, ReadOnlySpan<uint> offsets, ReadOnlySpan<byte> blob)
    {
        var vocab = (uint)lookup.Length;
        if (lookup[0] >= vocab)
        {
            return false;
        }

        for (var i = 1; i < lookup.Length; i++)
        {
            if (lookup[i] >= vocab)
            {
                return false;
            }

            var previous = TokenBytes(lookup[i - 1], offsets, blob);
            if (previous.SequenceCompareTo(TokenBytes(lookup[i], offsets, blob)) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    static ReadOnlySpan<byte> TokenBytes(uint id, ReadOnlySpan<uint> offsets, ReadOnlySpan<byte> blob) =>
        blob[(int)offsets[(int)id]..(int)offsets[(int)id + 1]];
}
