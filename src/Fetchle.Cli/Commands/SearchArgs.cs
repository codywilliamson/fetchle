using Fetchle.Cli.Output;
using Fetchle.Core.Search;

namespace Fetchle.Cli.Commands;

sealed class SearchArgs
{
    const int AGENT_DEFAULT_LIMIT = 20;

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

    public OutputMode OutputMode(CliEnvironment env) =>
        OutputModes.Detect(Json, Plain, env.StdoutRedirected, env.GetVariable);

    public SearchRequest ToRequest(OutputMode mode, CliEnvironment env) => new(
        Query,
        Roots.Count > 0 ? Roots : [env.CurrentDirectory],
        Limit ?? (mode == Output.OutputMode.Agent ? AGENT_DEFAULT_LIMIT : SearchRequest.DEFAULT_LIMIT),
        Budget,
        Extensions,
        Since is { } since ? SinceCutoff(since, env.Clock.GetUtcNow()) : null,
        Type);

    internal static DateTimeOffset SinceCutoff(TimeSpan since, DateTimeOffset now) =>
        since >= now - DateTimeOffset.MinValue ? DateTimeOffset.MinValue : now - since;
}
