using Fetchle.Core.Search;

namespace Fetchle.Cli.Output;

public interface IResultWriter
{
    void Write(SearchResult result);
}

static class ResultWriters
{
    public static IResultWriter For(OutputMode mode, TextWriter stdout, string query, TimeProvider clock) => mode switch
    {
        OutputMode.Pretty => new PrettyResultWriter(query, clock),
        OutputMode.Json => new JsonResultWriter(stdout),
        OutputMode.Agent => new PlainResultWriter(stdout, footer: true),
        _ => new PlainResultWriter(stdout, footer: false),
    };
}
