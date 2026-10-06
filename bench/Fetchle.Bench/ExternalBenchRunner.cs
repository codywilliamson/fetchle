using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Fetchle.Bench;

public sealed class ExternalBenchRunner(ExternalBenchOptions options, ProcessTimer timer, ILogger<ExternalBenchRunner> logger)
{
    // p95 needs more than a handful of runs to mean anything
    const int TIMED_RUNS = 20;
    const string QUERY = "settings";
    const int RG_SUCCESS = 0;
    const int FETCHLE_SUCCESS = 0;
    const string RESULTS_FILE = "external.json";

    public void Run()
    {
        var fetchleExe = FindFetchle();
        var rgExe = FindRg();

        var results = new List<ExternalResult>();
        foreach (var shape in BenchCorpus.Shapes)
        {
            var root = BenchCorpus.Ensure(shape);
            foreach (var command in CommandsFor(root, fetchleExe, rgExe))
            {
                results.Add(Measure(command, shape));
            }
        }

        WriteReport(results);
    }

    string? FindFetchle()
    {
        if (File.Exists(options.FetchleExe))
        {
            return options.FetchleExe;
        }
        logger.SkippingFetchle(options.FetchleExe);
        return null;
    }

    string? FindRg()
    {
        var rgExe = ExecutableLocator.FindOnPath("rg");
        if (rgExe is null)
        {
            logger.SkippingRg();
        }
        return rgExe;
    }

    static List<ToolCommand> CommandsFor(string root, string? fetchleExe, string? rgExe)
    {
        var commands = new List<ToolCommand>();
        if (fetchleExe is not null)
        {
            string[] fetchleArgs = [QUERY, "--root", root, "--budget", "5m", "--plain"];
            commands.Add(new ToolCommand("fetchle query", fetchleExe, fetchleArgs, FETCHLE_SUCCESS));
        }
        if (rgExe is not null)
        {
            // both walk the whole tree and print only paths matching the query, case-insensitive
            string[] rgArgs = ["--files", "--hidden", "--no-ignore", "--iglob", $"*{QUERY}*", root];
            string[] rgSingleThreadArgs = ["-j1", .. rgArgs];
            commands.Add(new ToolCommand("rg --files --iglob", rgExe, rgArgs, RG_SUCCESS));
            commands.Add(new ToolCommand("rg -j1 --files --iglob", rgExe, rgSingleThreadArgs, RG_SUCCESS));
        }
        return commands;
    }

    ExternalResult Measure(ToolCommand command, string shape)
    {
        logger.TimingTool(command.Tool, shape);

        // one untimed warmup so every tool sees a warm cache
        timer.TimeRunMs(command);

        var runsMs = new long[TIMED_RUNS];
        for (var i = 0; i < TIMED_RUNS; i++)
        {
            runsMs[i] = timer.TimeRunMs(command);
        }

        var result = new ExternalResult(command.Tool, shape, Percentile(runsMs, 0.50), Percentile(runsMs, 0.95), runsMs);
        logger.MeasuredTool(result.Tool, result.Shape, result.P50Ms, result.P95Ms, result.RunsMs);
        return result;
    }

    // nearest rank
    static long Percentile(long[] values, double fraction)
    {
        var sorted = (long[])values.Clone();
        Array.Sort(sorted);
        return sorted[(int)Math.Ceiling(fraction * sorted.Length) - 1];
    }

    void WriteReport(List<ExternalResult> results)
    {
        var os = $"{RuntimeInformation.OSDescription}, {Environment.ProcessorCount} cpus";
        var report = new ExternalReport(Environment.MachineName, os, BenchCorpus.FILLER, results);
        var path = Path.Combine(options.ResultsDirectory, RESULTS_FILE);
        File.WriteAllText(path, JsonSerializer.Serialize(report, ExternalJson.Default.ExternalReport) + "\n");
        logger.WroteResults(path);
    }
}
