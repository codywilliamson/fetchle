using System.Buffers;
using Fetchle.Core.Index;
using Fetchle.Core.Ranking.Lexical;
using Fetchle.Core.Ranking.Semantic;
using Fetchle.Core.Search;

namespace Fetchle.Core.Ranking;

// one ranked entry, before it becomes a path
readonly record struct RankedEntry(int Entry, float Score, IndexScope Scope);

// answers a query from a snapshot. the expensive work is per unique segment (TermScores); then one forward pass
// over the entries, parents before children, carries each term's best ancestor match down the tree and scores
// every entry that matched anything into a top-k. retrievers each keep their own top-k; Fusion merges them
static class RankedQuery
{
    // each retriever's candidates, best first
    // LexicalCovered: some candidate matched every query word the lexicon knows
    public sealed record Outcome(List<RankedEntry> Lexical, List<RankedEntry> Semantic, int Total, bool Completed, bool LexicalCovered = false);

    // semantic is null when there's no model, no vectors for this snapshot, or nothing in the query the model knows
    // a cancelled token stops the pass at the next chunk like a spent deadline; the caller throws
    public static Outcome Run(Snapshot snapshot, SearchRequest request, QueryTerms terms, IReadOnlyList<IndexScope> scopes, Deadline deadline,
        SemanticScores? semantic = null, CancellationToken cancellationToken = default)
    {
        if (scopes.Count == 0 || deadline.IsExpired())
        {
            return new Outcome([], [], 0, scopes.Count == 0);
        }

        using var scores = TermScores.Build(snapshot.Lexicon, snapshot.SegmentCount, terms);
        var copies = CopyMarks.Build(snapshot);
        var count = snapshot.EntryCount;
        var state = ArrayPool<byte>.Shared.Rent(count);
        var depth = ArrayPool<byte>.Shared.Rent(count);
        var carried = ArrayPool<ulong>.Shared.Rent(count);
        var pathSums = semantic is null ? null : ArrayPool<float>.Shared.Rent(count);
        try
        {
            state.AsSpan(0, count).Clear();
            var first = int.MaxValue;
            foreach (var scope in scopes)
            {
                state[scope.Entry] = EntryPass.SCOPE;
                first = Math.Min(first, scope.Entry + 1);
            }
            first = Math.Max(first, snapshot.RootCount);

            var pass = new EntryPass(snapshot, request, scopes, scores, semantic, copies, state, depth, carried, pathSums, terms.WantsDirectory);
            var completed = pass.Run(first, deadline, cancellationToken);
            var lexicalHits = pass.Ranked(lexical: true);
            var semanticHits = pass.Ranked(lexical: false);
            if (!pass.AnyWordMatched && (semanticHits.Count == 0 || semanticHits[0].Score < RankingWeights.MIN_SEMANTIC_ALONE))
            {
                return new Outcome([], [], 0, completed);
            }
            return new Outcome(lexicalHits, semanticHits, pass.Total, completed, pass.Covered);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(state);
            ArrayPool<byte>.Shared.Return(depth);
            ArrayPool<ulong>.Shared.Return(carried);
            if (pathSums is not null)
            {
                ArrayPool<float>.Shared.Return(pathSums);
            }
        }
    }
}
