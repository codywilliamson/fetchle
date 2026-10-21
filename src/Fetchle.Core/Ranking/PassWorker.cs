using System.Runtime.Intrinsics;
using System.Text;
using Fetchle.Core.Index;
using Fetchle.Core.Ranking.Semantic;
using Fetchle.Core.Search;

namespace Fetchle.Core.Ranking;

// scores one range of rows inside a BFS level into its own heaps. one per thread, never shared while scanning
sealed class PassWorker(EntryPass pass)
{
    char[] _nameBuffer = new char[256];

    public EntryHeap Lexical { get; } = new(RankingWeights.CANDIDATES);

    public EntryHeap Semantic { get; } = new(RankingWeights.CANDIDATES);

    public int Total { get; private set; }

    public bool Covered { get; private set; }

    public bool AnyWordMatched { get; private set; }

    // spans are hoisted into locals: the properties that hand them out slice the mapping on every call
    public void Scan(int from, int to)
    {
        var snapshot = pass.Snapshot;
        var parents = snapshot.Parents;
        var names = snapshot.Names;
        var flags = snapshot.Flags;
        var roots = snapshot.RootCount;
        var wordCounts = snapshot.Lexicon.SegmentWordCounts;
        var packed = pass.Scores.Packed;
        var state = pass.State;
        var depth = pass.Depth;
        var carried = pass.Carried;
        var pathSums = pass.PathSums;
        var copies = pass.Copies;
        var semantic = pass.Semantic;
        var bySegment = semantic is null ? [] : semantic.BySegment.AsSpan();
        var norms = semantic is null ? [] : semantic.Vectors.Norms;
        var request = pass.Request;
        var filtered = request.Type != EntryType.Any || request.Extensions is { Count: > 0 };
        var wantsDirectory = pass.WantsDirectory;

        // an entry whose own name matched nothing scores exactly like its siblings that didn't either: only the
        // parent's carried matches, the depth and the copy discount count. siblings are contiguous, so one cache
        var cachedParent = -1;
        var cachedDiscount = 0f;
        var cachedMatch = 0f;
        var cachedFull = false;

        for (var i = from; i < to; i++)
        {
            var parent = (int)parents[i];
            var above = state[parent];
            if (above == 0)
            {
                continue;
            }

            var name = names[i];
            var copy = (above & EntryPass.COPY) != 0 || (copies is not null && CopyMarks.IsSet(copies, name));
            state[i] = copy ? (byte)(EntryPass.INSIDE | EntryPass.COPY) : EntryPass.INSIDE;
            var discount = copy ? RankingWeights.COPY_PENALTY : 1f;
            // a scope's own name isn't part of its entries' paths, so its matches don't carry down
            var atTop = above == EntryPass.SCOPE;
            var level = atTop ? 1 : Math.Min(depth[parent] + 1, byte.MaxValue);
            depth[i] = (byte)level;
            var own = packed[name];
            var ancestors = atTop ? 0 : carried[parent];
            carried[i] = MaxBytes(own, ancestors);
            var isDirectory = (flags[i] & SnapshotFormat.FLAG_DIRECTORY) != 0;
            if (wantsDirectory && !isDirectory)
            {
                discount *= RankingWeights.NOT_A_DIRECTORY;
            }
            var counted = false;
            if ((own | ancestors) != 0)
            {
                float match;
                bool full;
                if (own == 0 && parent == cachedParent && discount == cachedDiscount)
                {
                    match = cachedMatch;
                    full = cachedFull;
                }
                else
                {
                    match = LexicalScore(own, ancestors, wordCounts[(int)name], out full);
                    if (own == 0)
                    {
                        (cachedParent, cachedDiscount, cachedMatch, cachedFull) = (parent, discount, match, full);
                    }
                }

                AnyWordMatched |= match > 0;
                // a deep path matching one word of five scores below zero here and stays out of the lexical list:
                // on dev that floor is worth 1 point of top-1 and 1.3 of top-3 over keeping it
                var score = discount * match - RankingWeights.DEPTH_PENALTY * level;
                if (score > 0 && (!filtered || TypeAndExtensionMatch(i)))
                {
                    Total++;
                    counted = true;
                    Covered |= full;
                    if (Lexical.WouldKeep(score, i) && ModifiedInRange(i))
                    {
                        Lexical.Add(score, i);
                    }
                }
            }

            if (semantic is null)
            {
                continue;
            }

            // cosine of the query with the path's weighted centroid: name 2, parent 1, older ancestors 0.5. with A the
            // running sum of segment scores down the path, the numerator is 2 s(name) + 0.5 s(parent) + 0.5 A(parent)
            var mine = bySegment[(int)name];
            var hasParent = parent >= roots;
            var parentScore = hasParent ? bySegment[(int)names[parent]] : 0;
            var parentSum = hasParent ? pathSums![parent] : 0;
            pathSums![i] = parentSum + mine;
            var norm = norms[i];
            if (norm == 0 || isDirectory)
            {
                continue;
            }

            var cosine = (VectorBuilder.NAME_WEIGHT * mine + (VectorBuilder.PARENT_WEIGHT - VectorBuilder.ANCESTOR_WEIGHT) * parentScore
                + VectorBuilder.ANCESTOR_WEIGHT * parentSum) / norm;
            if (cosine < RankingWeights.MIN_SEMANTIC || (!counted && filtered && !TypeAndExtensionMatch(i)))
            {
                continue;
            }

            if (!counted)
            {
                Total++;
            }

            var semanticScore = discount * cosine;
            if (Semantic.WouldKeep(semanticScore, i) && ModifiedInRange(i))
            {
                Semantic.Add(semanticScore, i);
            }
        }
    }

    // the idf-weighted best hit per term, name hits shortened by the name's length and ancestor hits discounted,
    // times how many of the terms matched at all
    float LexicalScore(ulong own, ulong ancestors, byte nameWords, out bool full)
    {
        full = false;
        var scores = pass.Scores;
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

        full = matched == matchable;
        var coverage = (float)matched / matchable;
        return sum * MathF.Pow(coverage, RankingWeights.COVERAGE_POWER);
    }

    bool TypeAndExtensionMatch(int entry)
    {
        var request = pass.Request;
        var isDirectory = pass.Snapshot.IsDirectory(entry);
        if ((request.Type == EntryType.Files && isDirectory) || (request.Type == EntryType.Directories && !isDirectory))
        {
            return false;
        }
        return request.Extensions is not { Count: > 0 } extensions || ExtensionMatches(entry, extensions);
    }

    // only asked of entries that would make a top-k, so --since stats few files
    bool ModifiedInRange(int entry)
    {
        if (pass.Request.ModifiedSince is not { } since)
        {
            return true;
        }

        var snapshot = pass.Snapshot;
        var scope = pass.ScopeOf(entry);
        var fullPath = SnapshotPaths.FullPath(snapshot, entry, scope.Entry, scope.Path);
        return EntryStat.Read(fullPath, snapshot.IsDirectory(entry)).Modified >= since;
    }

    bool ExtensionMatches(int entry, IReadOnlyList<string> extensions)
    {
        var snapshot = pass.Snapshot;
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

    // per byte max, one pmaxub
    static ulong MaxBytes(ulong a, ulong b) =>
        Vector128.Max(Vector128.CreateScalar(a).AsByte(), Vector128.CreateScalar(b).AsByte()).AsUInt64().ToScalar();
}
