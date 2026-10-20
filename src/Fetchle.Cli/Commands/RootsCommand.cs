using Fetchle.Core.Storage;
using XenoAtom.CommandLine;

namespace Fetchle.Cli.Commands;

// fetchle roots [add|remove|list] [path]. edits config.json only; the snapshot catches up on the next index rebuild
static class RootsCommand
{
    public static Command Create(CliEnvironment env, ActionTracker tracker)
    {
        var addPaths = new List<string>();
        var removePaths = new List<string>();
        return new Command("roots", "Manage indexed roots: add, remove, list")
        {
            new HelpOption(),
            new Command("add", "Add a root to index")
            {
                new HelpOption(),
                { "<path>", "Directory to index", addPaths },
                (ctx, _) => tracker.Done(Add(ctx, env, addPaths[0])),
            },
            new Command("remove", "Stop indexing a root")
            {
                new HelpOption(),
                { "<path>", "Root to remove", removePaths },
                (ctx, _) => tracker.Done(Remove(ctx, env, removePaths[0])),
            },
            new Command("list", "Print one root per line")
            {
                new HelpOption(),
                (ctx, _) => tracker.Done(List(ctx, env)),
            },
            (ctx, _) => tracker.Done(List(ctx, env)),
        };
    }

    static ExitCode Add(CommandRunContext ctx, CliEnvironment env, string path)
    {
        var dirs = env.Dirs;
        if (!TryNormalize(ctx, env, path, out var root))
        {
            return ExitCode.Usage;
        }

        if (!Directory.Exists(root))
        {
            ctx.Error.WriteLine($"fetchle roots: not a directory: {path}");
            return ExitCode.Usage;
        }

        var roots = RootsConfig.Load(dirs.Config);
        foreach (var existing in roots)
        {
            if (RootsConfig.Covers(existing, root))
            {
                ctx.Error.WriteLine($"fetchle roots: already covered by {existing}");
                return ExitCode.Usage;
            }

            if (RootsConfig.Covers(root, existing))
            {
                ctx.Error.WriteLine($"fetchle roots: {root} contains the root {existing}, remove that first");
                return ExitCode.Usage;
            }
        }

        roots.Add(root);
        RootsConfig.Save(dirs.Config, roots);
        ctx.Out.WriteLine($"added {root}");
        ctx.Out.WriteLine("run `fetchle index rebuild` to index it; until then search walks it live");
        return ExitCode.Success;
    }

    static ExitCode Remove(CommandRunContext ctx, CliEnvironment env, string path)
    {
        var dirs = env.Dirs;
        if (!TryNormalize(ctx, env, path, out var root))
        {
            return ExitCode.Usage;
        }

        var roots = RootsConfig.Load(dirs.Config);
        var removed = roots.RemoveAll(r => r.Equals(root, Core.Walking.PathComparison.Default));
        if (removed == 0)
        {
            ctx.Error.WriteLine($"fetchle roots: not a root: {root}");
            return ExitCode.Usage;
        }

        RootsConfig.Save(dirs.Config, roots);
        ctx.Out.WriteLine($"removed {root}");
        return ExitCode.Success;
    }

    static ExitCode List(CommandRunContext ctx, CliEnvironment env)
    {
        foreach (var root in RootsConfig.Load(env.Dirs.Config))
        {
            ctx.Out.WriteLine(root);
        }
        return ExitCode.Success;
    }

    static bool TryNormalize(CommandRunContext ctx, CliEnvironment env, string path, out string root)
    {
        try
        {
            root = RootsConfig.Normalize(Path.Combine(env.CurrentDirectory, path));
            return true;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            ctx.Error.WriteLine($"fetchle roots: invalid path: {path}");
            root = "";
            return false;
        }
    }
}
