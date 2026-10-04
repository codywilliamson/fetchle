using Fetchle.Core.Search;

namespace Fetchle.Cli.Commands;

sealed class SearchArgs
{
    public List<string> QueryWords { get; } = [];
    public List<string> Roots { get; } = [];
    public List<string> Extensions { get; } = [];
    public int? Limit { get; set; }
    public TimeSpan Budget { get; set; } = SearchRequest.DefaultBudget;
    public TimeSpan? Since { get; set; }
    public EntryType Type { get; set; }
    public bool Json { get; set; }
    public bool Plain { get; set; }
    public bool Interactive { get; set; }

    public string Query => string.Join(' ', QueryWords);
}
