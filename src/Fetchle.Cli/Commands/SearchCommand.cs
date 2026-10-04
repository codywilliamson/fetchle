using System.Text;
using Fetchle.Cli.Output;
using Fetchle.Core.Naive;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;
using XenoAtom.CommandLine;

namespace Fetchle.Cli.Commands;

static class SearchCommand
{
    const int AgentDefaultLimit = 20;

    public static ValueTask<int> RunAsync(CommandRunContext ctx, SearchArgs args)
    {
        if (args.QueryWords.Count == 0)
        {
            ctx.Error.WriteLine("fetchle: missing <query>, see fetchle --help");
            return ValueTask.FromResult(ExitCodes.Usage);
        }

        var mode = OutputModes.Detect(args.Json, args.Plain, Console.IsOutputRedirected, Environment.GetEnvironmentVariable);
        var request = new SearchRequest(
            args.Query,
            args.Roots.Count > 0 ? args.Roots : [Environment.CurrentDirectory],
            args.Limit ?? (mode == OutputMode.Agent ? AgentDefaultLimit : SearchRequest.DefaultLimit),
            args.Budget,
            args.Extensions,
            args.Since is { } since ? DateTimeOffset.UtcNow - since : null,
            args.Type);

        SearchResult result;
        try
        {
            result = new NaiveFileSearch(PruneRules.Default).Search(request, CancellationToken.None);
        }
        catch (InvalidRootException e)
        {
            ctx.Error.WriteLine($"fetchle: {e.Message}");
            return ValueTask.FromResult(ExitCodes.Usage);
        }

        if (mode == OutputMode.Pretty)
        {
            PrettyOutput.Write(result, request.Query);
        }
        else
        {
            // explicit utf-8: redirected console output otherwise uses the oem code page on windows
            using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
            if (mode == OutputMode.Json) JsonOutput.Write(stdout, result);
            else PlainOutput.Write(stdout, result, footer: mode == OutputMode.Agent);
        }

        return ValueTask.FromResult(result.Hits.Count > 0 ? ExitCodes.Success
            : result.StoppedEarly == StopReasons.Budget ? ExitCodes.BudgetExpired
            : ExitCodes.NoResults);
    }
}
