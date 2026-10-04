using System.Diagnostics;
using System.IO.Enumeration;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;

namespace Fetchle.Core.Naive;

// PLACEHOLDER: walks every root on every query with NaiveWalker + SubstringRanker.
// no index, so GetStatus has nothing to report
public sealed class NaiveFileSearch(PruneRules prune) : IFileSearch
{
    public SearchResult Search(SearchRequest request, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        var roots = new string[request.Roots.Count];
        for (var i = 0; i < roots.Length; i++)
        {
            roots[i] = NormalizeRoot(request.Roots[i]);
            if (!Directory.Exists(roots[i])) throw InvalidRootException.NotFound(request.Roots[i]);
        }

        var deadline = Deadline.After(request.Budget, start);
        var completed = true;

        var ranker = new SubstringRanker(request.Query);
        var walker = new NaiveWalker(prune);
        var hits = new List<SearchHit>();
        var relative = new char[256];
        foreach (var root in roots)
        {
            if (!completed || cancellationToken.IsCancellationRequested) break;
            completed = walker.Walk(root, (ref entry) =>
            {
                if (!Matches(ref entry, request)) return;
                var relativeDir = entry.Directory[root.Length..].TrimStart(['/', '\\']);
                var length = relativeDir.Length + 1 + entry.FileName.Length;
                if (relative.Length < length) relative = new char[length * 2];
                relativeDir.CopyTo(relative);
                relative[relativeDir.Length] = Path.DirectorySeparatorChar;
                entry.FileName.CopyTo(relative.AsSpan(relativeDir.Length + 1));
                var path = relativeDir.IsEmpty ? entry.FileName : relative.AsSpan(0, length);

                var score = ranker.Score(path, entry.FileName);
                if (score > 0)
                    hits.Add(new SearchHit(entry.ToFullPath(), score, entry.IsDirectory ? null : entry.Length, entry.LastWriteTimeUtc));
            }, deadline);
        }
        cancellationToken.ThrowIfCancellationRequested();

        hits.Sort(static (a, b) =>
        {
            var c = b.Score.CompareTo(a.Score);
            if (c == 0) c = a.Path.Length.CompareTo(b.Path.Length);
            return c != 0 ? c : string.CompareOrdinal(a.Path, b.Path);
        });
        var shown = hits.Count > request.Limit ? hits.GetRange(0, request.Limit) : hits;
        var stoppedEarly = completed ? null : StopReasons.Budget;
        return new SearchResult(shown, hits.Count, Stopwatch.GetElapsedTime(start), stoppedEarly);
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

    static bool Matches(ref FileSystemEntry entry, SearchRequest request)
    {
        if (request.Type == EntryType.Files && entry.IsDirectory) return false;
        if (request.Type == EntryType.Directories && !entry.IsDirectory) return false;
        if (request.ModifiedSince is { } since && entry.LastWriteTimeUtc < since) return false;
        if (request.Extensions is { Count: > 0 } extensions)
        {
            var ext = Path.GetExtension(entry.FileName).TrimStart('.');
            var any = false;
            foreach (var e in extensions)
                if (ext.Equals(e.TrimStart('.'), StringComparison.OrdinalIgnoreCase)) { any = true; break; }
            if (!any) return false;
        }
        return true;
    }
}
