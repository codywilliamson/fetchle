using Fetchle.Core.Index;

namespace Fetchle.Core.Ranking.Semantic;

// what a vectors file holds, before it's laid out
sealed record VectorParts(byte[] Codes, ulong[] Empty, float[] Norms);

// embeds every unique segment once, in parallel, then gives every entry the length of its path's weighted
// centroid: name 2, parent 1, every other ancestor 0.5, the spike's best scorer. the length comes from pairwise
// code dots along the path, so no per-entry vector is ever materialized
static class VectorBuilder
{
    public const float NAME_WEIGHT = 2f;
    public const float PARENT_WEIGHT = 1f;
    public const float ANCESTOR_WEIGHT = 0.5f;
    const int MAX_NAME = 1024;
    const int MAX_DEPTH = 256;

    public static VectorParts Build(Snapshot snapshot, ITextEncoder encoder, CancellationToken cancellationToken = default)
    {
        var segments = snapshot.SegmentCount;
        var codes = new byte[(long)segments * SignCodes.BYTES];
        var empty = new ulong[VectorFormat.EmptyWords(segments)];
        var options = new ParallelOptions { CancellationToken = cancellationToken };
        Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(0, segments), options, range =>
        {
            var vector = new float[SignCodes.DIMENSIONS];
            var chars = new char[MAX_NAME];
            var lowered = new char[MAX_NAME];
            var text = new char[MAX_NAME * 2];
            for (var s = range.Item1; s < range.Item2; s++)
            {
                if (!EncodeSegment(snapshot, encoder, (uint)s, chars, lowered, text, vector))
                {
                    // bits of other segments share this word, so set it atomically
                    Interlocked.Or(ref empty[s >> 6], 1UL << (s & 63));
                    continue;
                }
                SignCodes.Encode(vector, codes.AsSpan(s * SignCodes.BYTES, SignCodes.BYTES));
            }
        });

        var norms = new float[snapshot.EntryCount];
        Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(snapshot.RootCount, snapshot.EntryCount), options, range =>
        {
            Span<uint> path = stackalloc uint[MAX_DEPTH];
            for (var i = range.Item1; i < range.Item2; i++)
            {
                norms[i] = CentroidLength(snapshot, codes, empty, i, path);
            }
        });
        return new VectorParts(codes, empty, norms);
    }

    public static bool IsEmpty(ReadOnlySpan<ulong> empty, uint segment) => (empty[(int)(segment >> 6)] & (1UL << (int)(segment & 63))) != 0;

    public static float Weight(int fromName) => fromName switch
    {
        0 => NAME_WEIGHT,
        1 => PARENT_WEIGHT,
        _ => ANCESTOR_WEIGHT,
    };

    static bool EncodeSegment(Snapshot snapshot, ITextEncoder encoder, uint segment, char[] chars, char[] lowered, char[] text, float[] vector)
    {
        var bytes = snapshot.Segment(segment);
        if (bytes.Length > MAX_NAME)
        {
            return false;
        }

        var count = System.Text.Encoding.UTF8.GetChars(bytes, chars);
        var length = EmbedText.Write(chars.AsSpan(0, count), lowered, text);
        if (length == 0 || !encoder.Encode(text.AsSpan(0, length), vector))
        {
            return false;
        }

        var mean = encoder.Mean;
        for (var d = 0; d < vector.Length; d++)
        {
            vector[d] -= mean[d];
        }
        return true;
    }

    // |sum of w_k v_k| over the entry's path, from pairwise dots of the +-1 codes (each a unit vector)
    static float CentroidLength(Snapshot snapshot, byte[] codes, ulong[] empty, int entry, Span<uint> path)
    {
        var parents = snapshot.Parents;
        var names = snapshot.Names;
        var length = 0;
        var hop = 0;
        for (var at = entry; at >= snapshot.RootCount && length < path.Length; at = (int)parents[at], hop++)
        {
            if (!IsEmpty(empty, names[at]))
            {
                // the top two bits remember how far from the name it sat, for its weight
                path[length++] = names[at] | ((uint)Math.Min(hop, 2) << 30);
            }
        }

        var squared = 0f;
        for (var a = 0; a < length; a++)
        {
            var wa = Weight((int)(path[a] >> 30));
            var ca = Code(codes, path[a] & 0x3FFF_FFFF);
            squared += wa * wa;
            for (var b = a + 1; b < length; b++)
            {
                squared += 2 * wa * Weight((int)(path[b] >> 30)) * SignCodes.Dot(ca, Code(codes, path[b] & 0x3FFF_FFFF));
            }
        }
        return MathF.Sqrt(Math.Max(squared, 0));
    }

    static ReadOnlySpan<byte> Code(byte[] codes, uint segment) => codes.AsSpan((int)segment * SignCodes.BYTES, SignCodes.BYTES);
}
