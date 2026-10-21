using System.Buffers;
using Fetchle.Core.Index;

namespace Fetchle.Core.Ranking.Semantic;

// the query's cosine with every unique segment, computed once per query. the entry pass turns these into path
// centroid scores with a running sum, so no vector is touched per entry
sealed class SemanticScores : IDisposable
{
    // below this many segments one thread is quicker than fanning out
    const int PARALLEL_FROM = 64 * 1024;
    const int MAX_QUERY = 1024;

    SemanticScores(float[] bySegment, SegmentVectors vectors)
    {
        BySegment = bySegment;
        Vectors = vectors;
    }

    // rented, at least SegmentCount long; empty segments score 0
    public float[] BySegment { get; }

    public SegmentVectors Vectors { get; }

    // null when the query has nothing the model knows
    public static SemanticScores? Build(Snapshot snapshot, SegmentVectors vectors, ITextEncoder encoder, string query)
    {
        var table = QueryTable(encoder, query);
        if (table is null)
        {
            return null;
        }

        var count = snapshot.SegmentCount;
        var scores = ArrayPool<float>.Shared.Rent(Math.Max(count, 1));
        if (count < PARALLEL_FROM)
        {
            Fill(vectors, table, scores, 0, count);
        }
        else
        {
            Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(0, count, count / Environment.ProcessorCount + 1),
                range => Fill(vectors, table, scores, range.Item1, range.Item2));
        }
        return new SemanticScores(scores, vectors);
    }

    // the query embedded like a name, centered the way segment codes were, back to unit length
    static float[]? QueryTable(ITextEncoder encoder, string query)
    {
        if (query.Length > MAX_QUERY)
        {
            query = query[..MAX_QUERY];
        }

        var lowered = new char[query.Length];
        var text = new char[query.Length * 2 + 1];
        var length = EmbedText.Write(query, lowered, text);
        var vector = new float[SignCodes.DIMENSIONS];
        if (length == 0 || !encoder.Encode(text.AsSpan(0, length), vector))
        {
            return null;
        }

        var mean = encoder.Mean;
        var squared = 0f;
        for (var d = 0; d < vector.Length; d++)
        {
            vector[d] -= mean[d];
            squared += vector[d] * vector[d];
        }

        // a query exactly at the model's mean has no direction left to compare
        if (!(squared > 0))
        {
            return null;
        }

        var scale = 1f / MathF.Sqrt(squared);
        for (var d = 0; d < vector.Length; d++)
        {
            vector[d] *= scale;
        }
        return SignCodes.QueryTable(vector);
    }

    static void Fill(SegmentVectors vectors, float[] table, float[] scores, int from, int to)
    {
        var empty = vectors.Empty;
        for (var s = from; s < to; s++)
        {
            scores[s] = VectorBuilder.IsEmpty(empty, (uint)s) ? 0 : SignCodes.Score(table, vectors.Code((uint)s));
        }
    }

    public void Dispose() => ArrayPool<float>.Shared.Return(BySegment);
}
