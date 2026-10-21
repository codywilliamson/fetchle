using System.Text;
using Fetchle.Cli.Output;
using Fetchle.Cli.Picker;
using Fetchle.Core.Index;
using Fetchle.Core.Ranking.Semantic;
using Fetchle.Core.Search;
using Fetchle.Core.Storage;
using Fetchle.Core.Walking;
using XenoAtom.CommandLine;
using XenoAtom.Terminal;

namespace Fetchle.Cli.Commands;

// fetchle -i [query]: opens one search session, then re-ranks into the picker on every keystroke
sealed class PickerCommand(CliEnvironment env, ITextEncoder? model)
{
    public const int RESULT_LIMIT = 50;

    public ExitCode Run(CommandRunContext ctx, SearchArgs args)
    {
        if (!OutputModes.AllowsLiveWidgets(args.OutputMode(env)))
        {
            // a live widget on redirected output would write its frames into the stream
            ctx.Error.WriteLine("fetchle: the picker needs a terminal, drop -i or --plain/--json to search without one");
            return ExitCode.Usage;
        }

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

        var present = configured.FindAll(Directory.Exists);
        if (args.Roots.Count == 0 && configured.Count > 0 && present.Count == 0)
        {
            ctx.Error.WriteLine($"fetchle: every configured root is missing: {string.Join(", ", configured)}");
            return ExitCode.Usage;
        }

        var template = args.ToRequest(OutputMode.Pretty, env, present) with { Limit = args.Limit ?? RESULT_LIMIT };

        SearchSession session;
        try
        {
            session = SearchSession.Open(new IndexStore(env.Dirs.Index), PruneRules.Default, model);
        }
        catch (IndexUnreadableException e)
        {
            ctx.Error.WriteLine($"fetchle: {e.Message}");
            ctx.Error.WriteLine("run `fetchle index rebuild`");
            return ExitCode.IndexMissing;
        }

        using (session)
        {
            var picker = new PickerApp(
                (query, ct) => session.Search(template with { Query = query }, ct, prefix: true),
                args.Query,
                PickerApp.RowsFor(Terminal.Instance.Size.Rows),
                env.Clock);

            var chosen = picker.Run(Terminal.Instance);
            if (chosen is null)
            {
                return ExitCode.NoResults;
            }

            using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
            stdout.WriteLine(chosen);
            return ExitCode.Success;
        }
    }
}
