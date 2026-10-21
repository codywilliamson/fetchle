using Fetchle.Core.Index;
using Fetchle.Core.Search;

namespace Fetchle.Core.Ranking;

// reciprocal rank fusion, k=60, over each retriever's candidates from every source searched (the snapshot and any
// live-walked tree). a retriever's lists from several sources merge by score first, so a walked root and an indexed
// one rank as if they were one tree. equal scores share a rank, and the final tie-break is the path, so the order
// never depends on which source or entry id a candidate came from
static class Fusion
{
    public static List<SearchHit> Fuse(IReadOnlyList<(Snapshot Snapshot, RankedQuery.Outcome Outcome)> sources, int limit)
    {
        var fused = new Dictionary<(int Source, int Entry), (float Score, IndexScope Scope)>();
        var covered = false;
        foreach (var (_, outcome) in sources)
        {
            covered |= outcome.LexicalCovered;
        }

        AddRetriever(sources, fused, lexical: true, 1f);
        AddRetriever(sources, fused, lexical: false, covered ? RankingWeights.SEMANTIC_WEIGHT_COVERED : RankingWeights.SEMANTIC_WEIGHT);

        var candidates = new List<(float Score, string Path, int Source, int Entry)>(fused.Count);
        foreach (var ((source, entry), (score, scope)) in fused)
        {
            var snapshot = sources[source].Snapshot;
            candidates.Add((score, SnapshotPaths.FullPath(snapshot, entry, scope.Entry, scope.Path), source, entry));
        }
        candidates.Sort((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score) : string.CompareOrdinal(a.Path, b.Path));

        var hits = new List<SearchHit>(Math.Min(limit, candidates.Count));
        for (var c = 0; c < candidates.Count && hits.Count < limit; c++)
        {
            var (score, path, source, entry) = candidates[c];
            var stat = EntryStat.Read(path, sources[source].Snapshot.IsDirectory(entry));
            hits.Add(new SearchHit(path, score, stat.Size, stat.Modified));
        }
        return hits;
    }

    static void AddRetriever(IReadOnlyList<(Snapshot Snapshot, RankedQuery.Outcome Outcome)> sources,
        Dictionary<(int, int), (float Score, IndexScope Scope)> fused, bool lexical, float weight)
    {
        var merged = new List<(RankedEntry Ranked, int Source)>();
        for (var s = 0; s < sources.Count; s++)
        {
            var outcome = sources[s].Outcome;
            foreach (var ranked in lexical ? outcome.Lexical : outcome.Semantic)
            {
                merged.Add((ranked, s));
            }
        }
        merged.Sort((a, b) => b.Ranked.Score.CompareTo(a.Ranked.Score));

        // competition ranking: 1, 2, 2, 4
        var rank = 0;
        for (var i = 0; i < merged.Count && i < RankingWeights.CANDIDATES; i++)
        {
            if (i == 0 || merged[i].Ranked.Score != merged[i - 1].Ranked.Score)
            {
                rank = i + 1;
            }

            var (ranked, source) = merged[i];
            var key = (source, ranked.Entry);
            var contribution = weight / (RankingWeights.RRF_K + rank);
            fused[key] = fused.TryGetValue(key, out var existing) ? (existing.Score + contribution, ranked.Scope) : (contribution, ranked.Scope);
        }
    }
}
