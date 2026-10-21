using Fetchle.Cli.Commands;
using Fetchle.Cli.Install;
using Fetchle.Core.Index;
using Fetchle.Core.Storage;
using Fetchle.Core.Walking;

namespace Fetchle.Cli.Doctor;

// runs the fixes named in FixCommands.Runnable in-process, so it works from any build
internal static class DoctorFixes
{
    // true when the fix ran. a false comes with its reason in output
    public static bool Run(string command, CliEnvironment env, TextWriter output)
    {
        try
        {
            switch (command)
            {
                case FixCommands.SETUP:
                    SetupAction.Run(env, output);
                    return true;
                case FixCommands.INDEX_REBUILD:
                    return Rebuild(env, output);
                default:
                    return false;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or IndexBusyException or IndexUnreadableException)
        {
            output.WriteLine($"  failed: {e.Message}");
            return false;
        }
    }

    static bool Rebuild(CliEnvironment env, TextWriter output)
    {
        var dirs = env.Dirs;
        var roots = RootsConfig.Load(dirs.Config);
        if (roots.Count == 0)
        {
            output.WriteLine("  skipped: no roots configured");
            return false;
        }

        using var model = Commands.ModelLoader.ForRebuild(output);
        var report = new IndexStore(dirs.Index).Rebuild(roots, PruneRules.Default, model, CancellationToken.None);
        output.WriteLine($"  indexed {report.Snapshot.Entries:N0} entries in {report.TotalTime.TotalMilliseconds:0} ms");
        return true;
    }
}
