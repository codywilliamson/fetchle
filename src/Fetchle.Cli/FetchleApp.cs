using System.Reflection;
using Fetchle.Core;
using Fetchle.Mcp;
using XenoAtom.CommandLine;

namespace Fetchle.Cli;

// xenoatom won't mix root positionals with subcommands, so args[0] picks one of two apps:
// a known command name goes to the commands app, anything else is a search
static class FetchleApp
{
    const string _ = "";

    static readonly (string Name, string Description)[] Commands =
    [
        ("roots", "Manage indexed roots: add, remove, list"),
        ("index", "Show index health or rebuild it: status, rebuild"),
        ("mcp", "Run the stdio MCP server"),
        ("setup", "Detect agents and register fetchle with them"),
        ("doctor", "Check the install and print a fix for each failure"),
        ("update", "Self-update from GitHub releases"),
        ("cleanup", "Prune stale index data and leftover files"),
        ("uninstall", "Undo everything the install receipt records"),
        ("savings", "Show time and tokens saved"),
        ("help", "Help for a command"),
    ];

    static string Version => typeof(FetchleApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    public static async Task<int> RunAsync(string[] args)
    {
        var actionRan = false;
        var app = args.Length > 0 && IsCommand(args[0])
            ? CreateCommandsApp(() => actionRan = true)
            : CreateSearchApp(() => actionRan = true);
        var exitCode = await app.RunAsync(args);
        // the parser reports bad args as 1, which the spec reserves for "no results"
        return exitCode != ExitCodes.Success && !actionRan ? ExitCodes.Usage : exitCode;
    }

    static bool IsCommand(string arg)
    {
        foreach (var (name, _) in Commands)
            if (name == arg) return true;
        return false;
    }

    static CommandApp CreateSearchApp(Action onAction)
    {
        var search = new SearchArgs();
        var app = new CommandApp("fetchle")
        {
            new CommandUsage("Usage: {NAME} [options] <query>"),
            _,
            { "i", "Live picker, re-ranks as you type", _ => search.Interactive = true },
            { "limit=", "Max {N} results (10 on a TTY, 20 for agents)", (int v) => search.Limit = v },
            { "budget=", "Stop after {DURATION} and report what was skipped (default 2s)", v => search.Budget = Durations.Parse(v, "budget") },
            { "root=", "Restrict to {PATH}, repeatable (default: current directory)", search.Roots },
            { "ext=", "Only this {EXT}ension, repeatable", search.Extensions },
            { "since=", "Modified within {DURATION}, e.g. 3d", v => search.Since = Durations.Parse(v, "since") },
            { "type=", "{f|d}: files or directories only", v => search.Type = ParseType(v) },
            { "json", "One JSON object per line", _ => search.Json = true },
            { "plain", "Force plain output on a TTY", _ => search.Plain = true },
            new HelpOption(),
            new VersionOption(Version),
            _,
            "Arguments:",
            { "<query>*", "Plain-words description or partial name", search.QueryWords },
            _,
            "Commands:",
        };
        foreach (var (name, description) in Commands)
            app.Add($"  {name,-27}{description}");
        app.Add((ctx, _) =>
        {
            onAction();
            return search.Interactive ? NotImplemented(ctx, "-i") : SearchCommand.RunAsync(ctx, search);
        });
        return app;
    }

    static CommandApp CreateCommandsApp(Action onAction)
    {
        var helpTarget = new List<string>();
        var app = new CommandApp("fetchle")
        {
            Stub("roots", onAction),
            Stub("index", onAction),
            new Command("mcp", Describe("mcp"))
            {
                new HelpOption(),
                async (_, _) =>
                {
                    onAction();
                    // default root is the working directory until roots ship
                    var tools = new Tools(new NaiveFileSearch(PruneRules.Default), Environment.CurrentDirectory);
                    await McpServerHost.RunAsync(tools, Version, CancellationToken.None);
                    return ExitCodes.Success;
                },
            },
            Stub("setup", onAction),
            Stub("doctor", onAction, "fix", "Run the fixes"),
            Stub("update", onAction),
            Stub("cleanup", onAction, "yes", "Don't ask for confirmation"),
            Stub("uninstall", onAction, "yes", "Don't ask for confirmation"),
            Stub("savings", onAction),
        };
        app.Add(new Command("help", Describe("help"))
        {
            new HelpOption(),
            { "<command>?", "Command name", helpTarget },
            (ctx, _) =>
            {
                onAction();
                return ValueTask.FromResult(ShowHelp(ctx, app, helpTarget));
            },
        });
        return app;
    }

    static Command Stub(string name, Action onAction, string? flag = null, string? flagHelp = null)
    {
        var command = new Command(name, Describe(name)) { new HelpOption() };
        if (flag is not null) command.Add(flag, flagHelp!, _ => { });
        command.Add("<args>*", "Arguments", new List<string>());
        command.Add((ctx, _) =>
        {
            onAction();
            return NotImplemented(ctx, name);
        });
        return command;
    }

    static string Describe(string name)
    {
        foreach (var (n, description) in Commands)
            if (n == name) return description;
        throw new ArgumentException(name);
    }

    static ValueTask<int> NotImplemented(CommandRunContext ctx, string name)
    {
        ctx.Error.WriteLine($"fetchle {name}: not implemented yet");
        return ValueTask.FromResult(ExitCodes.Usage);
    }

    // long tldr-style help isn't written yet, so this shows the command's --help
    static int ShowHelp(CommandRunContext ctx, CommandApp commandsApp, List<string> target)
    {
        if (target.Count == 0)
        {
            CreateSearchApp(() => { }).ShowHelp(ctx.RunConfig);
            return ExitCodes.Success;
        }
        foreach (var node in commandsApp)
        {
            if (node is Command c && c.Name == target[0])
            {
                c.ShowHelp(ctx.RunConfig);
                return ExitCodes.Success;
            }
        }
        ctx.Error.WriteLine($"fetchle help: unknown command '{target[0]}'");
        return ExitCodes.Usage;
    }

    static EntryType ParseType(string? value) => value switch
    {
        "f" => EntryType.Files,
        "d" => EntryType.Directories,
        _ => throw new CommandOptionException($"invalid type '{value}', use f or d", "type"),
    };
}
