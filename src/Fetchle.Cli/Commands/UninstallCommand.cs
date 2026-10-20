using Fetchle.Cli.Install;
using XenoAtom.CommandLine;

namespace Fetchle.Cli.Commands;

// fetchle uninstall [--yes]. undoes exactly what the receipt lists and nothing else
static class UninstallCommand
{
    public static Command Create(CliEnvironment env, ActionTracker tracker)
    {
        var yes = false;
        return new Command("uninstall", "Undo everything the install receipt records")
        {
            new HelpOption(),
            { "yes", "Don't ask for confirmation", _ => yes = true },
            (ctx, _) => tracker.Done(Run(ctx, env, yes)),
        };
    }

    static ExitCode Run(CommandRunContext ctx, CliEnvironment env, bool yes)
    {
        var dirs = env.Dirs;
        Receipt? receipt;
        try
        {
            receipt = ReceiptStore.Load(dirs.Receipt);
        }
        catch (InvalidDataException e)
        {
            ctx.Error.WriteLine($"fetchle uninstall: {e.Message}");
            return Refuse(ctx, dirs.Bin, dirs.Data);
        }

        if (receipt is null)
        {
            ctx.Error.WriteLine($"fetchle uninstall: no receipt at {dirs.Receipt}, so I can't tell what was installed");
            return Refuse(ctx, dirs.Bin, dirs.Data);
        }

        if (ChannelCommands.UninstallCommand(receipt.Channel) is { } managerCommand)
        {
            ctx.Out.WriteLine($"fetchle was installed by a package manager ({receipt.Channel}). run: {managerCommand}");
            return ExitCode.Success;
        }

        var plan = UninstallPlan.From(receipt, PathPersistence.Home(env));
        if (plan.Problem is not null)
        {
            ctx.Error.WriteLine($"fetchle uninstall: refusing, {plan.Problem}");
            return ExitCode.InstallProblem;
        }

        return Confirm(ctx, plan, yes) ? Execute(ctx, plan) : ExitCode.Usage;
    }

    static bool Confirm(CommandRunContext ctx, UninstallPlan plan, bool yes)
    {
        if (yes)
        {
            return true;
        }

        ctx.Out.WriteLine("this will:");
        foreach (var step in plan.Steps)
        {
            ctx.Out.WriteLine($"  {step}");
        }

        if (Console.IsInputRedirected)
        {
            ctx.Error.WriteLine("fetchle uninstall: not a terminal, pass --yes to go ahead");
            return false;
        }

        ctx.Out.Write("continue? [y/N] ");
        return Console.ReadLine()?.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase) == true;
    }

    static ExitCode Execute(CommandRunContext ctx, UninstallPlan plan)
    {
        var receipt = plan.Receipt;
        if (receipt.PathEdit is { } edit)
        {
            var persistence = PathPersistence.FromReceipt(edit);
            if (persistence is null)
            {
                ctx.Error.WriteLine($"fetchle uninstall: can't undo a '{edit.Kind}' PATH edit on this OS, leaving it");
            }
            else
            {
                persistence.Remove(edit.Entry);
                ctx.Out.WriteLine(edit.File is null ? "removed fetchle from the user PATH" : $"removed the fetchle block from {edit.File}");
            }
        }

        var result = TreeRemoval.Remove([receipt.InstallDir, receipt.DataDir]);
        if (result.Remaining.Count == 0)
        {
            ctx.Out.WriteLine("removed fetchle");
            return ExitCode.Success;
        }

        if (result.CleanupSpawned)
        {
            ctx.Out.WriteLine($"removed fetchle, except {string.Join(", ", result.Remaining)}, which hold the running exe. a background cleanup deletes them within seconds of this process exiting");
            return ExitCode.Success;
        }

        ctx.Error.WriteLine($"fetchle uninstall: couldn't delete {string.Join(", ", result.Remaining)}");
        return ExitCode.InstallProblem;
    }

    static ExitCode Refuse(CommandRunContext ctx, string bin, string data)
    {
        ctx.Error.WriteLine("nothing was changed. to remove fetchle by hand:");
        ctx.Error.WriteLine($"  1. delete {bin}");
        ctx.Error.WriteLine($"  2. delete {data}");
        ctx.Error.WriteLine(OperatingSystem.IsWindows()
            ? $"  3. remove {bin} from your user PATH (Settings > System > About > Advanced system settings > Environment Variables)"
            : $"  3. delete the block between '{MarkerBlock.START}' and '{MarkerBlock.END}' from your shell rc file (~/.zshrc, ~/.bashrc or ~/.profile)");
        return ExitCode.InstallProblem;
    }
}
