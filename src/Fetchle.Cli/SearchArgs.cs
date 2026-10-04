namespace Fetchle.Cli;

enum SearchType { Any, Files, Directories }

sealed class SearchArgs
{
    public List<string> QueryWords { get; } = [];
    public List<string> Roots { get; } = [];
    public List<string> Extensions { get; } = [];
    public int? Limit { get; set; }
    public TimeSpan Budget { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan? Since { get; set; }
    public SearchType Type { get; set; }
    public bool Json { get; set; }
    public bool Plain { get; set; }
    public bool Interactive { get; set; }

    public string Query => string.Join(' ', QueryWords);
}
