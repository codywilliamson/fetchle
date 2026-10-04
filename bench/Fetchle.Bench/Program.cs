using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Fetchle.Bench;
using Microsoft.Extensions.Logging;

// usage: Fetchle.Bench [--external] [benchmarkdotnet args, e.g. --filter *Walk*]
// results land as json in bench/results/
using var loggerFactory = CreateLoggerFactory();
var logger = loggerFactory.CreateLogger("Fetchle.Bench");

var repo = RepoRoot();
var resultsDir = Path.Combine(repo, "bench", "results");
Directory.CreateDirectory(resultsDir);

if (args is ["--external", ..])
{
    ExternalBench.Run(repo, resultsDir);
    return 0;
}

var artifacts = Path.Combine(repo, "artifacts", "bench");
// whole walks take seconds on the dir-heavy shape, so few iterations are enough and keep runs bounded
var job = Job.Default.WithWarmupCount(1).WithIterationCount(5);
var config = DefaultConfig.Instance.WithArtifactsPath(artifacts).AddExporter(JsonExporter.Brief).AddJob(job);
BenchmarkSwitcher.FromAssembly(typeof(WalkBenchmarks).Assembly).Run(args.Length == 0 ? ["--filter", "*"] : args, config);

foreach (var report in Directory.EnumerateFiles(Path.Combine(artifacts, "results"), "*-report-brief.json"))
{
    var target = Path.Combine(resultsDir, Path.GetFileName(report).Replace("-report-brief", ""));
    File.Copy(report, target, overwrite: true);
    logger.WroteResults(target);
}
return 0;

static ILoggerFactory CreateLoggerFactory()
{
    return LoggerFactory.Create(builder =>
    {
        // benchmarkdotnet owns stdout, so log lines go to stderr
        builder.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
    });
}

static string RepoRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "fetchle.slnx")))
        {
            return dir.FullName;
        }
    }
    throw new InvalidOperationException("can't find fetchle.slnx above " + AppContext.BaseDirectory);
}
