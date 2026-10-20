using System.Diagnostics;
using Fetchle.Core.Index;
using Fetchle.Core.Naive;
using Fetchle.Core.Storage;
using Fetchle.Core.Walking;

namespace Fetchle.Core.Search;

// answers from the snapshot for every root it covers and walks the rest live, merged into one top-k.
// a snapshot that exists but fails validation throws IndexUnreadableException rather than guessing
public sealed class IndexedFileSearch(PruneRules prune, IndexStore store) : IFileSearch
{
    readonly FileSearch _walker = new(prune);

    public SearchResult Search(SearchRequest request, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        var roots = new List<string>(request.Roots.Count);
        foreach (var requested in request.Roots)
        {
            var root = FileSearch.NormalizeRoot(requested);
            if (!Directory.Exists(root))
            {
                throw InvalidRootException.NotFound(requested);
            }
            roots.Add(root);
        }

        using var snapshot = store.Open();
        var scopes = new List<IndexScope>();
        var walked = new List<string>();
        foreach (var root in roots)
        {
            var entry = snapshot is null ? -1 : SnapshotPaths.FindDirectory(snapshot, root);
            if (entry < 0)
            {
                walked.Add(root);
            }
            else if (!scopes.Exists(s => RootsConfig.Covers(s.Path, root)))
            {
                // a root under one already searched would only repeat its entries
                scopes.RemoveAll(s => RootsConfig.Covers(root, s.Path));
                scopes.Add(new IndexScope(entry, root));
            }
        }

        var deadline = Deadline.After(request.Budget, start);
        var hits = new TopHits(request.Limit);
        var total = 0;
        var completed = snapshot is null || IndexQuery.Run(snapshot, request, scopes, hits, ref total, deadline);
        cancellationToken.ThrowIfCancellationRequested();

        if (completed && walked.Count > 0)
        {
            var remaining = request.Budget - Stopwatch.GetElapsedTime(start);
            var live = _walker.Search(request with { Roots = walked, Budget = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining }, cancellationToken);
            total += live.TotalMatches;
            foreach (var hit in live.Hits)
            {
                if (hits.WouldKeep(hit.Score, hit.Path.Length))
                {
                    hits.Add(hit);
                }
            }
            completed = live.StoppedEarly is null;
        }

        return new SearchResult(hits.ToSortedList(), total, Stopwatch.GetElapsedTime(start), completed ? null : StopReasons.BUDGET);
    }

    public IndexStatus GetStatus(IReadOnlyList<string> roots)
    {
        var info = store.ReadInfo();
        var status = new List<RootStatus>(roots.Count);
        foreach (var root in roots)
        {
            var full = Path.GetFullPath(root);
            var indexed = info?.Roots.FirstOrDefault(r => RootsConfig.Covers(r.Path, full));
            status.Add(new RootStatus(full, indexed?.Entries, indexed is null ? null : info!.BuiltAt));
        }
        return new IndexStatus(status, VectorsComplete: false, WatcherLive: false);
    }
}
