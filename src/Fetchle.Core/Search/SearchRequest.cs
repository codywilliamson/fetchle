namespace Fetchle.Core.Search;

public enum EntryType { Any, Files, Directories }

public sealed record SearchRequest(
    string Query,
    IReadOnlyList<string> Roots,
    int Limit,
    TimeSpan Budget,
    IReadOnlyList<string>? Extensions = null,
    DateTimeOffset? ModifiedSince = null,
    EntryType Type = EntryType.Any)
{
    public const int DEFAULT_LIMIT = 10;
    public const int DEFAULT_BUDGET_MS = 2000;
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromMilliseconds(DEFAULT_BUDGET_MS);
}
