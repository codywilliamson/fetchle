using XenoAtom.CommandLine;

namespace Fetchle.Cli.Commands;

// PLACEHOLDER: the install lifecycle work fills this in
static class DoctorCommand
{
    public static Command Create(CliEnvironment env, ActionTracker tracker) => new("doctor", "Check the install and print a fix for each failure")
    {
        new HelpOption(),
        { "fix", "Run the fixes", _ => { } },
        { "<args>*", "Arguments", new List<string>() },
        (ctx, _) =>
        {
            ctx.Error.WriteLine("fetchle doctor: not implemented yet");
            return tracker.Done(ExitCode.Usage);
        },
    };
}
