using XenoAtom.CommandLine;

namespace Fetchle.Cli.Commands;

// PLACEHOLDER: the install lifecycle work fills this in
static class SetupCommand
{
    public static Command Create(CliEnvironment env, ActionTracker tracker) => new("setup", "Detect agents and register fetchle with them")
    {
        new HelpOption(),
        { "<args>*", "Arguments", new List<string>() },
        (ctx, _) =>
        {
            ctx.Error.WriteLine("fetchle setup: not implemented yet");
            return tracker.Done(ExitCode.Usage);
        },
    };
}
