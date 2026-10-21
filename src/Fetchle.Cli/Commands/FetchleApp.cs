using Fetchle.Core.Ranking.Semantic;
using Fetchle.Core.Search;
using Fetchle.Core.Storage;
using Fetchle.Mcp;
using XenoAtom.CommandLine;

namespace Fetchle.Cli.Commands;

sealed record CommandInfo(string Name, string Description);

sealed class FetchleApp(IFileSearch search, CliEnvironment env, ITextEncoder? model = null)
{
    const string BLANK_LINE = "";

    static readonly CommandInfo[] Commands =
    [
        new("roots", "Manage indexed roots: add, remove, list"),
        new("index", "Show index health or rebuild it: status, rebuild"),
        new("mcp", "Run the stdio MCP server"),
        new("setup", "Detect agents and register fetchle with them"),
        new("doctor", "Check the install and print a fix for each failure"),
        new("update", "Self-update from GitHub releases"),
        new("cleanup", "Prune stale index data and leftover files"),
        new("uninstall", "Undo everything the install receipt records"),
        new("savings", "Show time and tokens saved"),
        new("help", "Help for a command"),
    ];

    readonly ActionTracker _tracker = new();

    public async Task<ExitCode> RunAsync(string[] args)
    {
        var app = args.Length > 0 && Find(args[0]) is not null ? CreateCommandsApp() : CreateSearchApp();
        ExitCode exitCode;
        try
        {
            exitCode = (ExitCode)await app.RunAsync(args);
        }
        catch (InvalidDataException e)
        {
            // a hand-edited or truncated config or receipt. every command reads them, doctor included
            Console.Error.WriteLine($"fetchle: {e.Message}");
            Console.Error.WriteLine("fix or delete that file and try again");
            return ExitCode.Usage;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"fetchle: {e.Message}");
            return ExitCode.InstallProblem;
        }

        // the parser reports bad args as 1, which is our "no results"
        return exitCode != ExitCode.Success && !_tracker.Ran ? ExitCode.Usage : exitCode;
    }

    CommandApp CreateSearchApp()
    {
        var args = new SearchArgs();
        var app = new CommandApp("fetchle")
        {
            new CommandUsage("Usage: {NAME} [options] <query>"),
            BLANK_LINE,
            { "i", "Live picker, re-ranks as you type", _ => args.Interactive = true },
            { "limit=", "Max {N} results (10 on a TTY, 20 for agents)", (int v) => args.Limit = ParseLimit(v) },
            { "budget=", "Stop after {DURATION} and report what was skipped (default 2s)", v => args.Budget = Durations.Parse(v, "budget") },
            { "root=", "Restrict to {PATH}, repeatable (default: configured roots, else the current directory)", args.Roots },
            { "ext=", "Only this {EXT}ension, repeatable", args.Extensions },
            { "since=", "Modified within {DURATION}, e.g. 3d", v => args.Since = Durations.Parse(v, "since") },
            { "type=", "{f|d}: files or directories only", v => args.Type = ParseType(v) },
            { "json", "One JSON object per line", _ => args.Json = true },
            { "plain", "Force plain output on a TTY", _ => args.Plain = true },
            new HelpOption(),
            new VersionOption(env.Version),
            BLANK_LINE,
            "Arguments:",
            { "<query>*", "Plain-words description or partial name", args.QueryWords },
            BLANK_LINE,
            "Commands:",
        };

        foreach (var command in Commands)
        {
            app.Add($"  {command.Name,-27}{command.Description}");
        }

        app.Add((ctx, _) => _tracker.Done(args.Interactive ? new PickerCommand(env, model).Run(ctx, args) : new SearchCommand(search, env).Run(ctx, args)));
        return app;
    }

    CommandApp CreateCommandsApp()
    {
        var app = new CommandApp("fetchle")
        {
            RootsCommand.Create(env, _tracker),
            IndexCommand.Create(env, _tracker),
            Mcp(),
            SetupCommand.Create(env, _tracker),
            DoctorCommand.Create(env, _tracker),
            Stub("update"),
            Stub("cleanup", "yes", "Don't ask for confirmation"),
            UninstallCommand.Create(env, _tracker),
            Stub("savings"),
        };
        app.Add(Help(app));
        return app;
    }

    Command Mcp() => new("mcp", Describe("mcp"))
    {
        new HelpOption(),
        async (_, _) =>
        {
            await _tracker.Done(ExitCode.Success);
            var tools = new Tools(search, () =>
            {
                var configured = RootsConfig.Load(env.Dirs.Config);
                return configured.Count > 0 ? configured : [env.CurrentDirectory];
            });
            await new FetchleMcpServer(tools, env.Version).RunAsync(CancellationToken.None);
            return (int)ExitCode.Success;
        },
    };

    Command Help(CommandApp commandsApp)
    {
        var target = new List<string>();
        return new Command("help", Describe("help"))
        {
            new HelpOption(),
            { "<command>?", "Command name", target },
            (ctx, _) => _tracker.Done(ShowHelp(ctx, commandsApp, target)),
        };
    }

    Command Stub(string name, string? flag = null, string? flagHelp = null)
    {
        var command = new Command(name, Describe(name)) { new HelpOption() };
        if (flag is not null)
        {
            command.Add(flag, flagHelp!, _ => { });
        }

        command.Add("<args>*", "Arguments", new List<string>());
        command.Add((ctx, _) => _tracker.Done(NotImplemented(ctx, name)));
        return command;
    }

    // long tldr-style help isn't written yet, so this shows the command's --help
    ExitCode ShowHelp(CommandRunContext ctx, CommandApp commandsApp, List<string> target)
    {
        if (target.Count == 0)
        {
            CreateSearchApp().ShowHelp(ctx.RunConfig);
            return ExitCode.Success;
        }

        foreach (var node in commandsApp)
        {
            if (node is Command command && command.Name == target[0])
            {
                command.ShowHelp(ctx.RunConfig);
                return ExitCode.Success;
            }
        }

        ctx.Error.WriteLine($"fetchle help: unknown command '{target[0]}'");
        return ExitCode.Usage;
    }

    static ExitCode NotImplemented(CommandRunContext ctx, string name)
    {
        ctx.Error.WriteLine($"fetchle {name}: not implemented yet");
        return ExitCode.Usage;
    }

    static CommandInfo? Find(string name) => Array.Find(Commands, command => command.Name == name);

    static string Describe(string name) => Find(name)?.Description ?? throw new ArgumentException(name);

    static int ParseLimit(int value) =>
        value >= 1 ? value : throw new CommandOptionException("limit must be at least 1", "limit");

    static EntryType ParseType(string? value) => value switch
    {
        "f" => EntryType.Files,
        "d" => EntryType.Directories,
        _ => throw new CommandOptionException($"invalid type '{value}', use f or d", "type"),
    };
}
