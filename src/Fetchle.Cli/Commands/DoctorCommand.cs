using Fetchle.Cli.Doctor;
using XenoAtom.CommandLine;

namespace Fetchle.Cli.Commands;

// fetchle doctor [--fix]. exits 0 unless a check failed, then 5
static class DoctorCommand
{
    public static Command Create(CliEnvironment env, ActionTracker tracker)
    {
        var fix = false;
        return new Command("doctor", "Check the install and print a fix for each failure")
        {
            new HelpOption(),
            { "fix", "Run the fixes", _ => fix = true },
            async (ctx, _) => await tracker.Done(await Run(ctx, env, fix)),
        };
    }

    static async Task<ExitCode> Run(CommandRunContext ctx, CliEnvironment env, bool fix)
    {
        var results = await DoctorChecks.GatherAsync(env);
        Print(ctx.Out, results);
        if (fix && RunFixes(ctx.Out, env, results))
        {
            ctx.Out.WriteLine();
            ctx.Out.WriteLine("after fixing:");
            results = await DoctorChecks.GatherAsync(env);
            Print(ctx.Out, results);
        }

        return results.Any(r => r.Status == CheckStatus.Fail) ? ExitCode.InstallProblem : ExitCode.Success;
    }

    // each distinct command once, however many checks asked for it. true when any ran
    static bool RunFixes(TextWriter output, CliEnvironment env, List<CheckResult> results)
    {
        var commands = results
            .Where(r => r.Status is CheckStatus.Fail or CheckStatus.Warn && r.Fix is not null && FixCommands.Runnable.Contains(r.Fix))
            .Select(r => r.Fix!)
            .Distinct()
            .ToList();
        foreach (var command in commands)
        {
            output.WriteLine();
            output.WriteLine($"fixing: {command}");
            DoctorFixes.Run(command, env, output);
        }

        return commands.Count > 0;
    }

    static void Print(TextWriter output, List<CheckResult> results)
    {
        var width = results.Max(r => r.Name.Length);
        foreach (var result in results)
        {
            output.WriteLine($"{result.Status.ToString().ToLowerInvariant(),-5} {result.Name.PadRight(width)}  {result.Detail}");
            if (result.Fix is not null)
            {
                output.WriteLine($"{new string(' ', 6 + width)}  fix: {result.Fix}");
            }
        }

        var counts = Enum.GetValues<CheckStatus>().Select(s => $"{results.Count(r => r.Status == s)} {s.ToString().ToLowerInvariant()}");
        output.WriteLine(string.Join(", ", counts));
    }
}
