using System.ComponentModel;
using Fetchle.Core.Search;

namespace Fetchle.Mcp;

// optional members use set, not init: source gen puts init members in the object initializer and absent ones get default(T)
sealed record FindFilesArgs
{
    [Description("Plain-words description or partial name.")]
    public required string Query { get; init; }

    [Description("Max results, at least 1.")]
    [DefaultValue(SearchRequest.DEFAULT_LIMIT)]
    public int Limit { get; set; } = SearchRequest.DEFAULT_LIMIT;

    [Description("Hard time cap in ms, clamped to 25000.")]
    [DefaultValue(SearchRequest.DEFAULT_BUDGET_MS)]
    public int BudgetMs { get; set; } = SearchRequest.DEFAULT_BUDGET_MS;

    [Description("Restrict to one root. Null or absent means the default root.")]
    public string? Root { get; set; }

    [Description("Extension filter.")]
    public string[]? Ext { get; set; }
}

sealed record IndexStatusArgs;
