using Fetchle.Core.Naive;
using Fetchle.Core.Walking;

namespace Fetchle.Core.Search;

// one walker worker's private ranking state: no locks, and nothing allocates for entries that don't match
sealed class HitCollector(SearchRequest request, SubstringRanker ranker, string root, int limit)
{
    char[] _relative = new char[256];

    public TopHits Hits { get; } = new(limit);

    public int Total { get; private set; }

    public void Visit(ref WalkEntry entry)
    {
        if (!TypeAndExtensionMatch(ref entry))
        {
            return;
        }

        var relativeDir = entry.Directory[root.Length..].TrimStart(['/', '\\']);
        var length = relativeDir.Length + 1 + entry.FileName.Length;
        if (_relative.Length < length)
        {
            _relative = new char[length * 2];
        }

        relativeDir.CopyTo(_relative);
        _relative[relativeDir.Length] = Path.DirectorySeparatorChar;
        entry.FileName.CopyTo(_relative.AsSpan(relativeDir.Length + 1));
        var relativePath = relativeDir.IsEmpty ? entry.FileName : _relative.AsSpan(0, length);

        var score = ranker.Score(relativePath, entry.FileName);
        if (score <= 0)
        {
            return;
        }

        // the walker carries no mtime, so --since costs one stat per name match
        string? fullPath = null;
        if (request.ModifiedSince is { } since)
        {
            fullPath = entry.ToFullPath();
            if (EntryStat.Read(fullPath, entry.IsDirectory).Modified < since)
            {
                return;
            }
        }

        Total++;
        // only build the full path (and stat it) for hits that make this worker's cut
        var fullLength = entry.Directory.Length + (Path.EndsInDirectorySeparator(entry.Directory) ? 0 : 1) + entry.FileName.Length;
        if (Hits.WouldKeep(score, fullLength))
        {
            fullPath ??= entry.ToFullPath();
            var stat = EntryStat.Read(fullPath, entry.IsDirectory);
            Hits.Add(new SearchHit(fullPath, score, stat.Size, stat.Modified));
        }
    }

    bool TypeAndExtensionMatch(ref WalkEntry entry)
    {
        if (request.Type == EntryType.Files && entry.IsDirectory)
        {
            return false;
        }

        if (request.Type == EntryType.Directories && !entry.IsDirectory)
        {
            return false;
        }

        if (request.Extensions is not { Count: > 0 } extensions)
        {
            return true;
        }

        var ext = Path.GetExtension(entry.FileName).TrimStart('.');
        foreach (var e in extensions)
        {
            if (ext.Equals(e.TrimStart('.'), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
