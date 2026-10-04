#:package XenoAtom.Terminal
#:property RestorePackagesWithLockFile=false

using System.Diagnostics;
using XenoAtom.Terminal;

return BuildScript.Run(args);

static class BuildScript
{
    const int EXIT_USAGE = 2;

    public static int Run(string[] args)
    {
        if (args is ["-h"] or ["--help"])
        {
            Report.Usage();
            return 0;
        }

        var options = Options.Parse(args);
        var steps = options is null ? null : Targets.For(options);
        if (options is null || steps is null)
        {
            Report.Usage();
            return EXIT_USAGE;
        }

        Report.Banner(options);
        var results = Runner.RunAll(steps);
        Report.Summary(results);
        return Runner.ExitCode(results);
    }
}

sealed record Options(string Target, string? Rid)
{
    public static Options? Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        string? rid = null;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] != "--rid" || i + 1 >= args.Length)
            {
                return null;
            }
            rid = args[++i];
        }
        return new Options(args[0], rid);
    }
}

sealed record Step(string Name, string[] Args, IReadOnlyDictionary<string, string>? Environment = null)
{
    public string CommandLine()
    {
        var env = Environment is null ? "" : string.Concat(Environment.Select(pair => $"{pair.Key}={pair.Value} "));
        return $"{env}dotnet {string.Join(' ', Args.Select(Quote))}";
    }

    static string Quote(string arg)
    {
        if (arg.Contains(' '))
        {
            return $"\"{arg}\"";
        }
        return arg;
    }
}

enum Outcome
{
    Passed,
    Failed,
    Skipped,
}

sealed record StepResult(Step Step, Outcome Outcome, int ExitCode, TimeSpan Elapsed);

static class Targets
{
    const string SOLUTION = "fetchle.slnx";
    const string CLI = "src/Fetchle.Cli";
    const string UNIT_TESTS = "tests/Fetchle.Tests";
    const string E2E_TESTS = "tests/Fetchle.E2E";
    const string EVALS = "evals/Fetchle.Evals";
    const string BENCH = "bench/Fetchle.Bench";
    const string PUBLISH_DIR = "artifacts/publish";
    const string CONFIGURATION = "Release";

    public static readonly string[] NAMES = ["restore", "build", "test", "e2e", "eval", "publish", "bench", "ci"];

    public static Step[]? For(Options options) => options.Target switch
    {
        "restore" => [Restore(locked: false)],
        "build" => [Build(noRestore: false)],
        "test" => [UnitTests(noBuild: false)],
        "e2e" => [E2eTests(noBuild: false, exe: null)],
        "eval" => [Evals(noBuild: false)],
        "publish" => [Publish(options.Rid)],
        "bench" => Bench(options.Rid),
        "ci" => Ci(options.Rid),
        _ => null,
    };

    static Step[] Ci(string? rid) =>
    [
        Restore(locked: true),
        Build(noRestore: true),
        Publish(rid),
        UnitTests(noBuild: true),
        E2eTests(noBuild: true, exe: PublishedExe(rid)),
        Evals(noBuild: true),
    ];

    static Step[] Bench(string? rid) =>
    [
        Publish(rid),
        new("bench", ["run", "--project", BENCH, "-c", CONFIGURATION]),
        new("bench external", ["run", "--project", BENCH, "-c", CONFIGURATION, "--", "--external"], Env("FETCHLE_EXE", PublishedExe(rid))),
    ];

    static Step Restore(bool locked)
    {
        string[] args = ["restore", SOLUTION];
        return new("restore", locked ? [.. args, "--locked-mode"] : args);
    }

    static Step Build(bool noRestore) => new("build", WithFlag(["build", SOLUTION, "-c", CONFIGURATION], "--no-restore", noRestore));

    static Step UnitTests(bool noBuild) => new("unit", WithFlag(["test", "--project", UNIT_TESTS, "-c", CONFIGURATION], "--no-build", noBuild));

    static Step E2eTests(bool noBuild, string? exe)
    {
        var args = WithFlag(["test", "--project", E2E_TESTS, "-c", CONFIGURATION], "--no-build", noBuild);
        return new("e2e", args, exe is null ? null : Env("FETCHLE_EXE", exe));
    }

    static Step Evals(bool noBuild) => new("evals", WithFlag(["run", "--project", EVALS, "-c", CONFIGURATION], "--no-build", noBuild));

    static Step Publish(string? rid)
    {
        string[] args = ["publish", CLI, "-c", CONFIGURATION, "-o", PUBLISH_DIR];
        return new("publish", rid is null ? args : [.. args, "-r", rid]);
    }

    static string PublishedExe(string? rid)
    {
        var windows = rid is null ? OperatingSystem.IsWindows() : rid.StartsWith("win", StringComparison.Ordinal);
        return Path.GetFullPath(Path.Combine(PUBLISH_DIR, windows ? "fetchle.exe" : "fetchle"), Repo.Root);
    }

    static string[] WithFlag(string[] args, string flag, bool enabled)
    {
        if (enabled)
        {
            return [.. args, flag];
        }
        return args;
    }

    static Dictionary<string, string> Env(string name, string value) => new() { [name] = value };
}

static class Runner
{
    public static List<StepResult> RunAll(Step[] steps)
    {
        var results = new List<StepResult>();
        var failed = false;
        for (var i = 0; i < steps.Length; i++)
        {
            if (failed)
            {
                results.Add(new StepResult(steps[i], Outcome.Skipped, 0, TimeSpan.Zero));
                continue;
            }

            Report.StepStarted(steps[i], i + 1, steps.Length);
            var result = RunOne(steps[i]);
            Report.StepFinished(result);
            results.Add(result);
            failed = result.Outcome == Outcome.Failed;
        }
        return results;
    }

    public static int ExitCode(List<StepResult> results)
    {
        foreach (var result in results)
        {
            if (result.Outcome == Outcome.Failed)
            {
                return result.ExitCode;
            }
        }
        return 0;
    }

    static StepResult RunOne(Step step)
    {
        var started = Stopwatch.GetTimestamp();
        using var process = Process.Start(StartInfo(step))!;
        process.WaitForExit();
        var outcome = process.ExitCode == 0 ? Outcome.Passed : Outcome.Failed;
        return new StepResult(step, outcome, process.ExitCode, Stopwatch.GetElapsedTime(started));
    }

    static ProcessStartInfo StartInfo(Step step)
    {
        var info = new ProcessStartInfo("dotnet", step.Args) { WorkingDirectory = Repo.Root };
        ChildPath.Apply(info);
        if (step.Environment is not null)
        {
            foreach (var (name, value) in step.Environment)
            {
                info.Environment[name] = value;
            }
        }
        return info;
    }
}

static class ChildPath
{
    const string VS_INSTALLER = @"Microsoft Visual Studio\Installer";

    public static void Apply(ProcessStartInfo info)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var installer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), VS_INSTALLER);
        var path = info.Environment["PATH"] ?? "";
        if (Directory.Exists(installer) && !path.Contains(installer, StringComparison.OrdinalIgnoreCase))
        {
            info.Environment["PATH"] = $"{path}{Path.PathSeparator}{installer}";
        }
    }
}

static class Repo
{
    public static readonly string Root = AppContext.GetData("EntryPointFileDirectoryPath") as string ?? Directory.GetCurrentDirectory();
}

static class Report
{
    const int RULE_WIDTH = 64;
    const int NAME_WIDTH = 16;
    const int RESULT_WIDTH = 9;

    public static void Usage()
    {
        Terminal.WriteMarkupLine($"[bold]usage:[/] dotnet build.cs <target> [[--rid <rid>]]");
        Terminal.WriteMarkupLine($"[bold]targets:[/] {string.Join(", ", Targets.NAMES)}");
        Flush();
    }

    public static void Banner(Options options)
    {
        var rid = options.Rid is null ? "" : $" --rid {options.Rid}";
        Terminal.WriteMarkupLine($"[bold cyan]fetchle[/] [bold]{options.Target}[/][gray]{rid}[/]");
        Flush();
    }

    public static void StepStarted(Step step, int index, int count)
    {
        var title = $"[{index}/{count}] {step.Name} ";
        Terminal.WriteLine();
        Terminal.WriteMarkupLine($"[bold cyan]{title}[/][gray]{new string('─', Math.Max(4, RULE_WIDTH - title.Length))}[/]");
        Terminal.WriteMarkupLine($"[gray]$[/] {step.CommandLine()}");
        Flush();
    }

    public static void StepFinished(StepResult result)
    {
        if (result.Outcome == Outcome.Passed)
        {
            Terminal.WriteMarkupLine($"[green]✓ {result.Step.Name}[/] [gray]{Elapsed(result.Elapsed)}[/]");
        }
        else
        {
            Terminal.WriteMarkupLine($"[bold red]✗ {result.Step.Name} exited {result.ExitCode}[/] [gray]{Elapsed(result.Elapsed)}[/]");
        }
        Flush();
    }

    public static void Summary(List<StepResult> results)
    {
        Terminal.WriteLine();
        Terminal.WriteMarkupLine($"[bold]{"step".PadRight(NAME_WIDTH)}{"result".PadRight(RESULT_WIDTH)}{"time",8}[/]");
        Terminal.WriteMarkupLine($"[gray]{new string('─', NAME_WIDTH + RESULT_WIDTH + 8)}[/]");
        var total = TimeSpan.Zero;
        foreach (var result in results)
        {
            SummaryRow(result);
            total += result.Elapsed;
        }
        Terminal.WriteMarkupLine($"[gray]{new string('─', NAME_WIDTH + RESULT_WIDTH + 8)}[/]");
        Terminal.WriteMarkupLine($"[bold]{"total".PadRight(NAME_WIDTH + RESULT_WIDTH)}{Elapsed(total),8}[/]");
        Flush();
    }

    static void SummaryRow(StepResult result)
    {
        var name = result.Step.Name.PadRight(NAME_WIDTH);
        var time = result.Outcome == Outcome.Skipped ? "-" : Elapsed(result.Elapsed);
        var label = result.Outcome switch
        {
            Outcome.Passed => "pass",
            Outcome.Failed => "fail",
            _ => "skipped",
        };
        var color = result.Outcome switch
        {
            Outcome.Passed => "green",
            Outcome.Failed => "bold red",
            _ => "yellow",
        };
        Terminal.WriteMarkupLine($"{name}[{color}]{label.PadRight(RESULT_WIDTH)}[/]{time,8}");
    }

    static string Elapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalMinutes >= 1)
        {
            return $"{(int)elapsed.TotalMinutes}m{elapsed.Seconds:00}s";
        }
        return $"{elapsed.TotalSeconds:0.0}s";
    }

    static void Flush()
    {
        Terminal.Out.Flush();
    }
}
