using Fetchle.Cli.Install;
using XenoAtom.CommandLine;

namespace Fetchle.Cli.Commands;

// fetchle setup [--yes]. v0.1 does PATH and the receipt; there are no prompts yet, so --yes only has to be accepted
static class SetupCommand
{
    public static Command Create(CliEnvironment env, ActionTracker tracker) => new("setup", "Put fetchle on PATH and write the install receipt")
    {
        new HelpOption(),
        { "yes", "Take the defaults, never prompt", _ => { } },
        (ctx, _) => tracker.Done(Run(ctx, env)),
    };

    static ExitCode Run(CommandRunContext ctx, CliEnvironment env)
    {
        try
        {
            SetupAction.Run(env, ctx.Out);
            return ExitCode.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ctx.Error.WriteLine($"fetchle setup: {e.Message}");
            return ExitCode.InstallProblem;
        }
    }
}
