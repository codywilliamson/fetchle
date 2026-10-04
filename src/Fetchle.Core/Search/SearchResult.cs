namespace Fetchle.Core.Search;

public sealed record SearchHit(string Path, double Score, long? Size, DateTimeOffset Modified);

public sealed record SearchResult(IReadOnlyList<SearchHit> Hits, int TotalMatches, TimeSpan Elapsed, string? StoppedEarly)
{
    // footer shared by agent mode and mcp text content, docs/specs/cli.md#output-modes
    public string Footer()
    {
        var footer = $"{TotalMatches} matches, showing {Hits.Count}, {(long)Elapsed.TotalMilliseconds}ms";
        return StoppedEarly is null ? footer : $"{footer}, stopped early: {StoppedEarly}";
    }
}

// wire values of stopped_early, docs/specs/mcp.md
public static class StopReasons
{
    public const string Budget = "budget";
    public const string Limit = "limit";
}
