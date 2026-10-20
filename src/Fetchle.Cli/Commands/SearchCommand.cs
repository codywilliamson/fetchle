using System.Text;
using Fetchle.Cli.Output;
using Fetchle.Core.Index;
using Fetchle.Core.Search;
using Fetchle.Core.Storage;
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
        List<string> configured;
        try
        {
            configured = RootsConfig.Load(env.Dirs.Config);
        }
        catch (InvalidDataException e)
        {
            ctx.Error.WriteLine($"fetchle: {e.Message}");
            return ExitCode.Usage;
        }

        // an unplugged drive or a deleted root shouldn't take every other root's search down with it
        var present = configured.FindAll(Directory.Exists);
        if (args.Roots.Count == 0 && configured.Count > 0 && present.Count == 0)
        {
            ctx.Error.WriteLine($"fetchle: every configured root is missing: {string.Join(", ", configured)}");
            return ExitCode.Usage;
        }

        if (args.Roots.Count == 0)
        {
            foreach (var missing in configured.Except(present))
            {
                ctx.Error.WriteLine($"fetchle: skipping missing root {missing}, `fetchle roots remove` it if it's gone for good");
            }
        }

        var request = args.ToRequest(mode, env, present);

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
        catch (IndexUnreadableException e)
        {
            ctx.Error.WriteLine($"fetchle: {e.Message}");
            ctx.Error.WriteLine("run `fetchle index rebuild`");
            return ExitCode.IndexMissing;
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
