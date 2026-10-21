using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace Fetchle.Core.Embedding;

// builds a .fmodel image from f32 weights and a vocab. public because tools/model/convert.cs is the only
// production caller; tests use it to make tiny models without the 129 MB download
public static class ModelWriter
{
    public const int DEFAULT_MAX_WORD_CHARS = 100;

    // convert.cs stamps its output with this so a format bump rebuilds the model
    public static uint FormatVersion => ModelFormat.VERSION;

    // tokens[i] is the token with id i, embeddings is row-major [tokens.Count x dims]
    public static byte[] Build(IReadOnlyList<string> tokens, ReadOnlySpan<float> embeddings, int dims, int unkId, string continuingPrefix, int maxWordChars = DEFAULT_MAX_WORD_CHARS)
    {
        var vocab = tokens.Count;
        if (embeddings.Length != (long)vocab * dims)
        {
            throw new ArgumentException($"{embeddings.Length} weights is not {vocab} x {dims}", nameof(embeddings));
        }

        var prefix = Encoding.UTF8.GetBytes(continuingPrefix);
        if (prefix.Length > ModelFormat.MAX_PREFIX_BYTES || (uint)unkId >= (uint)vocab)
        {
            throw new ArgumentException("prefix too long or unk id out of range");
        }

        var tokenBytes = new byte[vocab][];
        var offsets = new uint[vocab + 1];
        for (var i = 0; i < vocab; i++)
        {
            tokenBytes[i] = Encoding.UTF8.GetBytes(tokens[i]);
            offsets[i + 1] = offsets[i] + (uint)tokenBytes[i].Length;
        }

        var lookup = new uint[vocab];
        for (var i = 0; i < vocab; i++)
        {
            lookup[i] = (uint)i;
        }

        Array.Sort(lookup, (a, b) => tokenBytes[a].AsSpan().SequenceCompareTo(tokenBytes[b]));

        var maxAbs = 0f;
        foreach (var weight in embeddings)
        {
            maxAbs = Math.Max(maxAbs, Math.Abs(weight));
        }

        var scale = maxAbs > 0 ? maxAbs / ModelFormat.INT8_MAX : 1f;
        var lengths = new long[ModelFormat.SECTION_COUNT];
        lengths[ModelFormat.TOKEN_OFFSETS] = offsets.Length * 4L;
        lengths[ModelFormat.TOKEN_BLOB] = offsets[^1];
        lengths[ModelFormat.LOOKUP] = vocab * 4L;
        lengths[ModelFormat.EMBEDDINGS] = (long)vocab * dims;
        lengths[ModelFormat.MEAN] = dims * 4L;

        var starts = new long[ModelFormat.SECTION_COUNT];
        var end = (long)ModelFormat.HEADER_SIZE;
        for (var s = 0; s < starts.Length; s++)
        {
            starts[s] = ModelFormat.Align(end);
            end = starts[s] + lengths[s];
        }

        var image = new byte[ModelFormat.Align(end)];
        WriteHeader(image, dims, vocab, unkId, maxWordChars, scale, prefix, starts, lengths);
        MemoryMarshal.AsBytes(offsets.AsSpan()).CopyTo(image.AsSpan((int)starts[ModelFormat.TOKEN_OFFSETS]));
        var blob = image.AsSpan((int)starts[ModelFormat.TOKEN_BLOB]);
        for (var i = 0; i < vocab; i++)
        {
            tokenBytes[i].CopyTo(blob[(int)offsets[i]..]);
        }

        MemoryMarshal.AsBytes(lookup.AsSpan()).CopyTo(image.AsSpan((int)starts[ModelFormat.LOOKUP]));
        Quantize(embeddings, scale, MemoryMarshal.Cast<byte, sbyte>(image.AsSpan((int)starts[ModelFormat.EMBEDDINGS], vocab * dims)));
        MeanOfNormalizedRows(embeddings, dims, MemoryMarshal.Cast<byte, float>(image.AsSpan((int)starts[ModelFormat.MEAN], dims * 4)));
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(ModelFormat.CHECKSUM_OFFSET), ModelChecks.Checksum(image));
        return image;
    }

    // temp file then rename, so a half-written model is never at the final path
    public static void Write(string path, byte[] image)
    {
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, image);
        File.Move(temp, path, overwrite: true);
    }

    static void WriteHeader(byte[] image, int dims, int vocab, int unkId, int maxWordChars, float scale, byte[] prefix, long[] starts, long[] lengths)
    {
        var header = image.AsSpan();
        BinaryPrimitives.WriteUInt64LittleEndian(header[ModelFormat.MAGIC_OFFSET..], ModelFormat.MAGIC);
        BinaryPrimitives.WriteUInt32LittleEndian(header[ModelFormat.VERSION_OFFSET..], ModelFormat.VERSION);
        MemoryMarshal.Write(header[ModelFormat.ENDIAN_OFFSET..], ModelFormat.ENDIAN_MARKER);
        BinaryPrimitives.WriteUInt64LittleEndian(header[ModelFormat.LENGTH_OFFSET..], (ulong)image.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(header[ModelFormat.DIMS_OFFSET..], (uint)dims);
        BinaryPrimitives.WriteUInt32LittleEndian(header[ModelFormat.VOCAB_OFFSET..], (uint)vocab);
        BinaryPrimitives.WriteUInt32LittleEndian(header[ModelFormat.UNK_OFFSET..], (uint)unkId);
        BinaryPrimitives.WriteUInt32LittleEndian(header[ModelFormat.MAX_WORD_CHARS_OFFSET..], (uint)maxWordChars);
        BinaryPrimitives.WriteInt32LittleEndian(header[ModelFormat.SCALE_OFFSET..], BitConverter.SingleToInt32Bits(scale));
        BinaryPrimitives.WriteUInt32LittleEndian(header[ModelFormat.PREFIX_LENGTH_OFFSET..], (uint)prefix.Length);
        prefix.CopyTo(header[ModelFormat.PREFIX_OFFSET..]);
        for (var s = 0; s < starts.Length; s++)
        {
            var entry = header[(ModelFormat.SECTION_TABLE_OFFSET + s * ModelFormat.SECTION_ENTRY_SIZE)..];
            BinaryPrimitives.WriteUInt64LittleEndian(entry, (ulong)starts[s]);
            BinaryPrimitives.WriteUInt64LittleEndian(entry[8..], (ulong)lengths[s]);
        }
    }

    static void Quantize(ReadOnlySpan<float> weights, float scale, Span<sbyte> destination)
    {
        for (var i = 0; i < weights.Length; i++)
        {
            var q = MathF.Round(weights[i] / scale);
            destination[i] = (sbyte)Math.Clamp(q, -ModelFormat.INT8_MAX, ModelFormat.INT8_MAX);
        }
    }

    static void MeanOfNormalizedRows(ReadOnlySpan<float> weights, int dims, Span<float> mean)
    {
        var sum = new double[dims];
        var rows = weights.Length / dims;
        for (var r = 0; r < rows; r++)
        {
            var row = weights.Slice(r * dims, dims);
            double norm = 0;
            foreach (var weight in row)
            {
                norm += (double)weight * weight;
            }

            if (norm == 0)
            {
                continue;
            }

            var inverse = 1 / Math.Sqrt(norm);
            for (var d = 0; d < dims; d++)
            {
                sum[d] += row[d] * inverse;
            }
        }

        for (var d = 0; d < dims; d++)
        {
            mean[d] = (float)(sum[d] / rows);
        }
    }
}
