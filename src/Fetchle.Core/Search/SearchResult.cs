namespace Fetchle.Core.Search;

public sealed record SearchHit(string Path, double Score, long? Size, DateTimeOffset Modified);

public sealed record SearchResult(IReadOnlyList<SearchHit> Hits, int TotalMatches, TimeSpan Elapsed, string? StoppedEarly)
{
    public string Footer()
    {
        var footer = $"{TotalMatches} matches, showing {Hits.Count}, {(long)Elapsed.TotalMilliseconds}ms";
        return StoppedEarly is null ? footer : $"{footer}, stopped early: {StoppedEarly}";
    }
}

public static class StopReasons
{
    public const string BUDGET = "budget";
    public const string LIMIT = "limit";
}
