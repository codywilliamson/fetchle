using Fetchle.Core.Index;
using Fetchle.Core.Storage;
using Fetchle.Core.Walking;
using XenoAtom.CommandLine;

namespace Fetchle.Cli.Commands;

// fetchle index [status|rebuild]
static class IndexCommand
{
    public static Command Create(CliEnvironment env, ActionTracker tracker) => new("index", "Show index health or rebuild it: status, rebuild")
    {
        new HelpOption(),
        new Command("status", "Show snapshot age, size and roots")
        {
            new HelpOption(),
            (ctx, _) => tracker.Done(Status(ctx, env)),
        },
        new Command("rebuild", "Walk every root and swap in a fresh snapshot")
        {
            new HelpOption(),
            (ctx, _) => tracker.Done(Rebuild(ctx, env)),
        },
        (ctx, _) => tracker.Done(Status(ctx, env)),
    };

    static ExitCode Status(CommandRunContext ctx, CliEnvironment env)
    {
        var dirs = env.Dirs;
        var store = new IndexStore(dirs.Index);
        SnapshotInfo? info;
        try
        {
            info = store.ReadInfo();
        }
        catch (IndexUnreadableException e)
        {
            ctx.Error.WriteLine($"fetchle index: {e.Message}");
            ctx.Error.WriteLine("run `fetchle index rebuild`");
            return ExitCode.IndexMissing;
        }

        var configured = RootsConfig.Load(dirs.Config);
        if (info is null)
        {
            ctx.Error.WriteLine($"fetchle index: no index at {store.SnapshotPath}");
            ctx.Error.WriteLine(configured.Count == 0 ? "run `fetchle roots add <path>`, then `fetchle index rebuild`" : "run `fetchle index rebuild`");
            return ExitCode.IndexMissing;
        }

        var age = env.Clock.GetUtcNow() - info.BuiltAt;
        ctx.Out.WriteLine($"snapshot  {info.File}");
        ctx.Out.WriteLine($"built     {info.BuiltAt:u} ({Output.Humanize.Age(age)})");
        ctx.Out.WriteLine($"entries   {info.Entries:N0} ({info.Segments:N0} unique names)");
        ctx.Out.WriteLine($"size      {Output.Humanize.Size(info.Bytes)} ({info.BytesPerEntry:0.0} bytes per entry)");
        foreach (var root in info.Roots)
        {
            // still in use while a configured root is it or sits under it, which search answers from this snapshot too
            var state = configured.Exists(r => RootsConfig.Covers(root.Path, r)) ? "" : "  (no longer configured)";
            ctx.Out.WriteLine($"root      {root.Path}  {root.Files:N0} files, {root.Entries - root.Files:N0} dirs, walked in {root.WalkTime.TotalMilliseconds:0} ms{state}");
        }

        foreach (var root in configured)
        {
            var covering = info.Roots.FirstOrDefault(r => RootsConfig.Covers(r.Path, root));
            if (covering is null)
            {
                ctx.Out.WriteLine($"root      {root}  not indexed yet, searched live until `fetchle index rebuild`");
            }
            else if (!covering.Path.Equals(root, PathComparison.Default))
            {
                ctx.Out.WriteLine($"root      {root}  answered from {covering.Path}'s snapshot");
            }
        }
        return ExitCode.Success;
    }

    static ExitCode Rebuild(CommandRunContext ctx, CliEnvironment env)
    {
        var dirs = env.Dirs;
        var roots = RootsConfig.Load(dirs.Config);
        if (roots.Count == 0)
        {
            ctx.Error.WriteLine("fetchle index: no roots, run `fetchle roots add <path>` first");
            return ExitCode.Usage;
        }

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                ctx.Error.WriteLine($"fetchle index: root not found: {root}, run `fetchle roots remove {root}`");
                return ExitCode.Usage;
            }
        }

        RebuildReport report;
        try
        {
            report = new IndexStore(dirs.Index).Rebuild(roots, PruneRules.Default, CancellationToken.None);
        }
        catch (IndexBusyException e)
        {
            ctx.Error.WriteLine($"fetchle index: {e.Message}");
            return ExitCode.IndexMissing;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // disk full, a failed rename, a locked temp file: the old snapshot is still the one readers see
            ctx.Error.WriteLine($"fetchle index: couldn't write the snapshot, kept the old one: {e.Message}");
            return ExitCode.IndexMissing;
        }
        catch (IndexUnreadableException e)
        {
            // the new image failed its own checks before it was written, so the old snapshot is still in place
            ctx.Error.WriteLine($"fetchle index: rebuild produced a bad snapshot, kept the old one: {e.Reason}");
            return ExitCode.IndexMissing;
        }

        var info = report.Snapshot;
        ctx.Out.WriteLine($"indexed {info.Entries:N0} entries in {report.TotalTime.TotalMilliseconds:0} ms (walk {report.WalkTime.TotalMilliseconds:0}, build {report.BuildTime.TotalMilliseconds:0}, write {report.WriteTime.TotalMilliseconds:0})");
        ctx.Out.WriteLine($"{info.File}: {Output.Humanize.Size(info.Bytes)}, {info.BytesPerEntry:0.0} bytes per entry");
        return ExitCode.Success;
    }
}
