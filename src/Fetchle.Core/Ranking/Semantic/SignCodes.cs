using System.Numerics;
using System.Runtime.InteropServices;

namespace Fetchle.Core.Ranking.Semantic;

// a segment vector stored as the signs of its 512 centered dims: 64 bytes. read back it is +-1/sqrt(512) per dim,
// a unit vector. on the spike's 12 queries this kept rankings close to f32 (top-10 6 of 12 vs 5) where truncating
// to 64 dims lost the bundled claude binary (rank 7 -> 11,242). the query side stays float: a query scores a code
// through 64 lookup tables of 256 partial sums, one per code byte
static class SignCodes
{
    public const int DIMENSIONS = 512;
    public const int BYTES = DIMENSIONS / 8;
    const int WORDS = BYTES / sizeof(ulong);
    static readonly float Unit = 1f / MathF.Sqrt(DIMENSIONS);

    // bit d set when centered[d] >= 0
    public static void Encode(ReadOnlySpan<float> centered, Span<byte> code)
    {
        code[..BYTES].Clear();
        for (var d = 0; d < DIMENSIONS; d++)
        {
            if (centered[d] >= 0)
            {
                code[d >> 3] |= (byte)(1 << (d & 7));
            }
        }
    }

    // cosine of two codes read back as +-1 vectors: 1 - 2 * hamming / dims
    public static float Dot(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var x = MemoryMarshal.Cast<byte, ulong>(a[..BYTES]);
        var y = MemoryMarshal.Cast<byte, ulong>(b[..BYTES]);
        var differing = 0;
        for (var w = 0; w < WORDS; w++)
        {
            differing += BitOperations.PopCount(x[w] ^ y[w]);
        }
        return 1f - 2f * differing / DIMENSIONS;
    }

    // the query's lookup tables: entry [byte position][byte value] is the query's dot with those 8 signs
    public static float[] QueryTable(ReadOnlySpan<float> query)
    {
        var table = new float[BYTES * 256];
        for (var b = 0; b < BYTES; b++)
        {
            var row = table.AsSpan(b * 256, 256);
            for (var value = 0; value < 256; value++)
            {
                var sum = 0f;
                for (var bit = 0; bit < 8; bit++)
                {
                    var q = query[b * 8 + bit];
                    sum += (value & (1 << bit)) != 0 ? q : -q;
                }
                row[value] = sum * Unit;
            }
        }
        return table;
    }

    // a model of another width can't fill these codes; callers treat it as no model
    public static bool Fits(ITextEncoder? model) => model is not null && model.Dimensions == DIMENSIONS;

    // the query's cosine with one code, the code read back as a unit vector
    public static float Score(ReadOnlySpan<float> table, ReadOnlySpan<byte> code)
    {
        var sum = 0f;
        for (var b = 0; b < BYTES; b++)
        {
            sum += table[(b << 8) | code[b]];
        }
        return sum;
    }
}
