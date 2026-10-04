namespace Fetchle.Core;

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
    public const int DefaultLimit = 10;
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(2);
}
