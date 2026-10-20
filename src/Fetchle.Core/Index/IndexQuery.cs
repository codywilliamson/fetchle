using System.Buffers;
using System.Text;
using Fetchle.Core.Naive;
using Fetchle.Core.Search;

namespace Fetchle.Core.Index;

// one subtree of the snapshot to search, spelled the way the caller passed its root
readonly record struct IndexScope(int Entry, string Path);

// answers a query from the snapshot with the same matches, scores and order the walker would give on the same tree.
// segments are matched once each, then one forward pass over the entries carries "under a scope" and
// "path matches" down from parent to child; parents come before children, so a single pass sees every parent first
static class IndexQuery
{
    const byte SCOPE = 1;
    const byte INSIDE = 2;
    const byte PATH_MATCH = 4;
    const byte INSIDE_MATCHING = INSIDE | PATH_MATCH;

    // deadline checks cost a timestamp read, so the pass only looks every this many entries
    const int DEADLINE_STRIDE = 16 * 1024;

    // returns false when the deadline cut the pass short
    public static bool Run(Snapshot snapshot, SearchRequest request, IReadOnlyList<IndexScope> scopes, TopHits hits, ref int total, Deadline deadline)
    {
        if (scopes.Count == 0)
        {
            return true;
        }

        if (deadline.IsExpired())
        {
            return false;
        }

        var bySeparator = request.Query.Contains(Path.DirectorySeparatorChar);
        var bits = bySeparator ? null : SegmentMatcher.Match(snapshot, request.Query, out _);
        var state = ArrayPool<byte>.Shared.Rent(snapshot.EntryCount);
        try
        {
            // roots have no parent to look up, and nothing below the lowest scope can be inside one
            var first = int.MaxValue;
            state.AsSpan(0, snapshot.EntryCount).Clear();
            foreach (var scope in scopes)
            {
                state[scope.Entry] = SCOPE;
                first = Math.Min(first, scope.Entry + 1);
            }
            first = Math.Max(first, snapshot.RootCount);

            var pass = new Pass(snapshot, request, scopes, hits, state);
            var completed = bySeparator ? pass.RunByPath(first, deadline) : pass.RunBySegment(first, bits!, deadline);
            total += pass.Total;
            return completed;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(state);
            if (bits is not null)
            {
                ArrayPool<ulong>.Shared.Return(bits);
            }
        }
    }

    sealed class Pass(Snapshot snapshot, SearchRequest request, IReadOnlyList<IndexScope> scopes, TopHits hits, byte[] state)
    {
        public int Total { get; private set; }

        public bool RunBySegment(int first, ulong[] bits, Deadline deadline)
        {
            var parents = snapshot.Parents;
            var names = snapshot.Names;
            for (var i = first; i < parents.Length; i++)
            {
                if ((i & (DEADLINE_STRIDE - 1)) == 0 && deadline.IsExpired())
                {
                    return false;
                }

                var above = state[parents[i]];
                if (above == 0)
                {
                    continue;
                }

                // a scope's own name isn't part of its entries' relative paths, so only an INSIDE parent passes its match down
                var nameMatch = SegmentMatcher.IsSet(bits, names[i]);
                var pathMatch = nameMatch || (above & PATH_MATCH) != 0;
                state[i] = pathMatch ? INSIDE_MATCHING : INSIDE;
                if (pathMatch)
                {
                    Consider(i, nameMatch ? SubstringRanker.NAME_MATCH : SubstringRanker.PATH_MATCH);
                }
            }
            return true;
        }

        // a query holding a separator can only match a whole relative path, so each in-scope entry's path gets built
        public bool RunByPath(int first, Deadline deadline)
        {
            var parents = snapshot.Parents;
            var buffer = new char[1024];
            for (var i = first; i < parents.Length; i++)
            {
                if ((i & (DEADLINE_STRIDE - 1)) == 0 && deadline.IsExpired())
                {
                    return false;
                }

                if (state[parents[i]] == 0)
                {
                    continue;
                }

                state[i] = INSIDE;
                int length;
                while ((length = SnapshotPaths.RelativePath(snapshot, i, ScopeOf(i).Entry, buffer)) < 0)
                {
                    buffer = new char[buffer.Length * 2];
                }

                if (buffer.AsSpan(0, length).Contains(request.Query, StringComparison.OrdinalIgnoreCase))
                {
                    Consider(i, SubstringRanker.PATH_MATCH);
                }
            }
            return true;
        }

        void Consider(int entry, double score)
        {
            var isDirectory = snapshot.IsDirectory(entry);
            if ((request.Type == EntryType.Files && isDirectory) || (request.Type == EntryType.Directories && !isDirectory))
            {
                return;
            }

            if (request.Extensions is { Count: > 0 } extensions && !ExtensionMatches(entry, extensions))
            {
                return;
            }

            var scope = ScopeOf(entry);
            string? fullPath = null;
            if (request.ModifiedSince is { } since)
            {
                fullPath = SnapshotPaths.FullPath(snapshot, entry, scope.Entry, scope.Path);
                if (EntryStat.Read(fullPath, isDirectory).Modified < since)
                {
                    return;
                }
            }

            Total++;
            var length = SnapshotPaths.PathLength(snapshot, entry, scope.Entry, scope.Path.Length, Path.EndsInDirectorySeparator(scope.Path));
            if (hits.WouldKeep(score, length))
            {
                fullPath ??= SnapshotPaths.FullPath(snapshot, entry, scope.Entry, scope.Path);
                var stat = EntryStat.Read(fullPath, isDirectory);
                hits.Add(new SearchHit(fullPath, score, stat.Size, stat.Modified));
            }
        }

        // the nearest ancestor marked SCOPE. scopes never nest, so there's exactly one
        IndexScope ScopeOf(int entry)
        {
            if (scopes.Count == 1)
            {
                return scopes[0];
            }

            var parents = snapshot.Parents;
            var at = (int)parents[entry];
            while (state[at] != SCOPE)
            {
                at = (int)parents[at];
            }

            // indexed loops: a foreach over the interface would box an enumerator per matched entry
            for (var s = 0; s < scopes.Count; s++)
            {
                if (scopes[s].Entry == at)
                {
                    return scopes[s];
                }
            }
            throw new InvalidOperationException("entry outside every scope");
        }

        // one decode buffer per pass, grown once for the longest name seen, so long unicode names don't allocate per hit
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
    }
}
