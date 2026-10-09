using System.Diagnostics;
using Fetchle.Core.Naive;
using Fetchle.Core.Walking;

namespace Fetchle.Core.Search;

// walks with FastWalker, one HitCollector per worker, then merges the collectors into one top-k
public sealed class FileSearch(PruneRules prune) : IFileSearch
{
    public SearchResult Search(SearchRequest request, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        var roots = new string[request.Roots.Count];
        for (var i = 0; i < roots.Length; i++)
        {
            roots[i] = NormalizeRoot(request.Roots[i]);
            if (!Directory.Exists(roots[i]))
            {
                throw InvalidRootException.NotFound(request.Roots[i]);
            }
        }

        var deadline = Deadline.After(request.Budget, start);
        var completed = true;
        var ranker = new SubstringRanker(request.Query);
        var walker = new FastWalker(prune);
        var merged = new TopHits(request.Limit);
        var total = 0;

        foreach (var root in roots)
        {
            if (!completed || cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var collectors = new List<HitCollector>();
            completed = walker.Walk(root, () =>
            {
                var collector = new HitCollector(request, ranker, root, request.Limit);
                lock (collectors)
                {
                    collectors.Add(collector);
                }
                return collector.Visit;
            }, deadline);

            // every worker is done, so collectors are safe to read. top-k order is total, so merge order can't change the result
            foreach (var collector in collectors)
            {
                total += collector.Total;
                merged.AddAll(collector.Hits);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();

        var stoppedEarly = completed ? null : StopReasons.BUDGET;
        return new SearchResult(merged.ToSortedList(), total, Stopwatch.GetElapsedTime(start), stoppedEarly);
    }

    static string NormalizeRoot(string root)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw InvalidRootException.BadSyntax(root);
        }
    }

    public IndexStatus GetStatus(IReadOnlyList<string> roots) =>
        new([.. roots.Select(r => new RootStatus(Path.GetFullPath(r), FileCount: null, LastFullScan: null))], VectorsComplete: false, WatcherLive: false);
}
