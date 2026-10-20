using XenoAtom.CommandLine;

namespace Fetchle.Cli.Commands;

// PLACEHOLDER: the install lifecycle work fills this in
static class UninstallCommand
{
    public static Command Create(CliEnvironment env, ActionTracker tracker) => new("uninstall", "Undo everything the install receipt records")
    {
        new HelpOption(),
        { "yes", "Don't ask for confirmation", _ => { } },
        { "<args>*", "Arguments", new List<string>() },
        (ctx, _) =>
        {
            ctx.Error.WriteLine("fetchle uninstall: not implemented yet");
            return tracker.Done(ExitCode.Usage);
        },
    };
}
