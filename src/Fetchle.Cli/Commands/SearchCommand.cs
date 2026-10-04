using System.Text;
using Fetchle.Cli.Output;
using Fetchle.Core.Search;
using XenoAtom.CommandLine;

namespace Fetchle.Cli.Commands;

sealed class SearchCommand(IFileSearch search, CliEnvironment env)
{
    public ExitCode Run(CommandRunContext ctx, SearchArgs args)
    {
        if (args.QueryWords.Count == 0)
        {
            ctx.Error.WriteLine("fetchle: missing <query>, see fetchle --help");
            return ExitCode.Usage;
        }

        var mode = args.OutputMode(env);
        var request = args.ToRequest(mode, env);

        SearchResult result;
        try
        {
            result = search.Search(request, CancellationToken.None);
        }
        catch (InvalidRootException e)
        {
            ctx.Error.WriteLine($"fetchle: {e.Message}");
            return ExitCode.Usage;
        }

        // redirected console output on windows otherwise uses the oem code page
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        ResultWriters.For(mode, stdout, request.Query, env.Clock).Write(result);

        if (result.Hits.Count > 0)
        {
            return ExitCode.Success;
        }
        return result.StoppedEarly == StopReasons.BUDGET ? ExitCode.BudgetExpired : ExitCode.NoResults;
    }
}
