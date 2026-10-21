using System.Buffers;
using System.Runtime.Intrinsics;
using System.Text;
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
    const byte SCOPE = 1;
    const byte INSIDE = 2;
    // set on an entry at or below a scratch copy dir, see CopyMarks
    const byte COPY = 4;
    const int DEADLINE_STRIDE = 16 * 1024;

    // each retriever's candidates, best first
    public sealed record Outcome(List<RankedEntry> Lexical, List<RankedEntry> Semantic, int Total, bool Completed);

    // semantic is null when there's no model, no vectors for this snapshot, or nothing in the query the model knows
    public static Outcome Run(Snapshot snapshot, SearchRequest request, QueryTerms terms, IReadOnlyList<IndexScope> scopes, Deadline deadline, SemanticScores? semantic = null)
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
                state[scope.Entry] = SCOPE;
                first = Math.Min(first, scope.Entry + 1);
            }
            first = Math.Max(first, snapshot.RootCount);

            var pass = new Pass(snapshot, request, scopes, scores, semantic, copies, state, depth, carried, pathSums);
            pass.SeedScopes();
            var completed = pass.Run(first, deadline);
            return new Outcome(pass.Ranked(pass.Lexical), pass.Ranked(pass.Semantic), pass.Total, completed);
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

    sealed class Pass(Snapshot snapshot, SearchRequest request, IReadOnlyList<IndexScope> scopes, TermScores scores, SemanticScores? semantic,
        ulong[]? copies, byte[] state, byte[] depth, ulong[] carried, float[]? pathSums)
    {
        public EntryHeap Lexical { get; } = new(RankingWeights.CANDIDATES);

        public EntryHeap Semantic { get; } = new(RankingWeights.CANDIDATES);

        public int Total { get; private set; }

        public List<RankedEntry> Ranked(EntryHeap heap)
        {
            var sorted = heap.ToSorted();
            var ranked = new List<RankedEntry>(sorted.Length);
            foreach (var (score, entry) in sorted)
            {
                ranked.Add(new RankedEntry(entry, score, ScopeOf(entry)));
            }
            return ranked;
        }

        // a scope's path sum covers the ancestors above it too: centroids run over the whole path
        public void SeedScopes()
        {
            if (semantic is null)
            {
                return;
            }

            foreach (var scope in scopes)
            {
                var sum = 0f;
                for (var at = scope.Entry; at >= snapshot.RootCount; at = (int)snapshot.Parents[at])
                {
                    sum += semantic.BySegment[snapshot.Names[at]];
                }
                pathSums![scope.Entry] = sum;
            }
        }

        public bool Run(int first, Deadline deadline)
        {
            var parents = snapshot.Parents;
            var names = snapshot.Names;
            var packed = scores.Packed;
            for (var i = first; i < parents.Length; i++)
            {
                if ((i & (DEADLINE_STRIDE - 1)) == 0 && deadline.IsExpired())
                {
                    return false;
                }

                var parent = (int)parents[i];
                var above = state[parent];
                if (above == 0)
                {
                    continue;
                }

                var copy = (above & COPY) != 0 || (copies is not null && CopyMarks.IsSet(copies, names[i]));
                state[i] = copy ? (byte)(INSIDE | COPY) : INSIDE;
                var discount = copy ? RankingWeights.COPY_PENALTY : 1f;
                // a scope's own name isn't part of its entries' paths, so its matches don't carry down
                var atTop = above == SCOPE;
                depth[i] = atTop ? (byte)1 : (byte)Math.Min(depth[parent] + 1, byte.MaxValue);
                var own = packed[names[i]];
                var ancestors = atTop ? 0 : carried[parent];
                carried[i] = MaxBytes(own, ancestors);
                var counted = false;
                if ((own | ancestors) != 0)
                {
                    var score = discount * LexicalScore(own, ancestors, snapshot.Lexicon.SegmentWordCounts[(int)names[i]]) - RankingWeights.DEPTH_PENALTY * depth[i];
                    if (score > 0 && TypeAndExtensionMatch(i))
                    {
                        Total++;
                        counted = true;
                        if (Lexical.WouldKeep(score, i) && ModifiedInRange(i))
                        {
                            Lexical.Add(score, i);
                        }
                    }
                }

                if (semantic is not null)
                {
                    ScoreSemantic(i, parent, counted, discount);
                }
            }
            return true;
        }

        // cosine of the query with the path's weighted centroid: name 2, parent 1, older ancestors 0.5. with A the
        // running sum of segment scores down the path, the numerator is 2 s(name) + 0.5 s(parent) + 0.5 A(parent)
        void ScoreSemantic(int i, int parent, bool counted, float discount)
        {
            var bySegment = semantic!.BySegment;
            var mine = bySegment[snapshot.Names[i]];
            var hasParent = parent >= snapshot.RootCount;
            var parentScore = hasParent ? bySegment[snapshot.Names[parent]] : 0;
            var parentSum = hasParent ? pathSums![parent] : 0;
            pathSums![i] = parentSum + mine;

            var norm = semantic.Vectors.Norms[i];
            if (norm == 0)
            {
                return;
            }

            var cosine = (VectorBuilder.NAME_WEIGHT * mine + (VectorBuilder.PARENT_WEIGHT - VectorBuilder.ANCESTOR_WEIGHT) * parentScore
                + VectorBuilder.ANCESTOR_WEIGHT * parentSum) / norm;
            if (cosine < RankingWeights.MIN_SEMANTIC || (!counted && !TypeAndExtensionMatch(i)))
            {
                return;
            }

            if (!counted)
            {
                Total++;
            }

            var score = discount * cosine - RankingWeights.SEMANTIC_DEPTH_PENALTY * depth[i];
            if (Semantic.WouldKeep(score, i) && ModifiedInRange(i))
            {
                Semantic.Add(score, i);
            }
        }

        // the idf-weighted best hit per term, name hits shortened by the name's length and ancestor hits discounted,
        // times how many of the terms matched at all
        float LexicalScore(ulong own, ulong ancestors, byte nameWords)
        {
            var lengthNorm = 1f / (1 + RankingWeights.NAME_LENGTH_PENALTY * Math.Max(nameWords - 1, 0));
            var sum = 0f;
            var matched = 0;
            var matchable = 0;
            for (var t = 0; t < scores.TermCount; t++)
            {
                var weight = scores.Weights[t];
                if (weight == 0)
                {
                    continue;
                }

                matchable++;
                var name = (own >> (t * 8)) & 0xFF;
                var above = (ancestors >> (t * 8)) & 0xFF;
                var best = Math.Max(name * lengthNorm, above * RankingWeights.ANCESTOR_MATCH) / byte.MaxValue;
                if (best > 0)
                {
                    matched++;
                    sum += weight * best;
                }
            }

            if (matched == 0)
            {
                return 0;
            }

            var coverage = (float)matched / matchable;
            return sum * MathF.Pow(coverage, RankingWeights.COVERAGE_POWER);
        }

        bool TypeAndExtensionMatch(int entry)
        {
            var isDirectory = snapshot.IsDirectory(entry);
            if ((request.Type == EntryType.Files && isDirectory) || (request.Type == EntryType.Directories && !isDirectory))
            {
                return false;
            }
            return request.Extensions is not { Count: > 0 } extensions || ExtensionMatches(entry, extensions);
        }

        // only asked of entries that would make a top-k, so --since stats few files
        bool ModifiedInRange(int entry)
        {
            if (request.ModifiedSince is not { } since)
            {
                return true;
            }

            var scope = ScopeOf(entry);
            var fullPath = SnapshotPaths.FullPath(snapshot, entry, scope.Entry, scope.Path);
            return EntryStat.Read(fullPath, snapshot.IsDirectory(entry)).Modified >= since;
        }

        char[] _nameBuffer = new char[256];

        bool ExtensionMatches(int entry, IReadOnlyList<string> extensions)
        {
            var bytes = snapshot.Segment(snapshot.Names[entry]);
            if (bytes.Length > _nameBuffer.Length)
            {
                _nameBuffer = new char[bytes.Length];
            }

            var name = _nameBuffer.AsSpan(0, Encoding.UTF8.GetChars(bytes, _nameBuffer));
            var ext = Path.GetExtension(name).TrimStart('.');
            for (var e = 0; e < extensions.Count; e++)
            {
                if (ext.Equals(extensions[e].AsSpan().TrimStart('.'), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // the nearest ancestor marked SCOPE. scopes never nest, so there's exactly one
        public IndexScope ScopeOf(int entry)
        {
            if (scopes.Count == 1)
            {
                return scopes[0];
            }

            var parents = snapshot.Parents;
            var at = (int)parents[entry];
            while ((state[at] & SCOPE) == 0)
            {
                at = (int)parents[at];
            }

            for (var s = 0; s < scopes.Count; s++)
            {
                if (scopes[s].Entry == at)
                {
                    return scopes[s];
                }
            }
            throw new InvalidOperationException("entry outside every scope");
        }
    }

    // per byte max, one pmaxub
    static ulong MaxBytes(ulong a, ulong b) =>
        Vector128.Max(Vector128.CreateScalar(a).AsByte(), Vector128.CreateScalar(b).AsByte()).AsUInt64().ToScalar();
}
