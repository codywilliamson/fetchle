using System.ComponentModel;
using Fetchle.Core.Search;

namespace Fetchle.Mcp;

// optional members use set, not init: source gen puts init members in the object initializer and absent ones get default(T)
sealed record FindFilesArgs
{
    public const int DEFAULT_BUDGET_MS = 2000;

    [Description("Plain-words description or partial name.")]
    public required string Query { get; init; }

    [Description("Max results, at least 1.")]
    [DefaultValue(SearchRequest.DefaultLimit)]
    public int Limit { get; set; } = SearchRequest.DefaultLimit;

    [Description("Hard time cap in ms, clamped to 25000.")]
    [DefaultValue(DEFAULT_BUDGET_MS)]
    public int BudgetMs { get; set; } = DEFAULT_BUDGET_MS;

    [Description("Restrict to one root. Null or absent means the default root.")]
    public string? Root { get; set; }

    [Description("Extension filter.")]
    public string[]? Ext { get; set; }
}

sealed record IndexStatusArgs;
