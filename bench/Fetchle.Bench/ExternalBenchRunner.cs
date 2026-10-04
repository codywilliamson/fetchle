using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Fetchle.Bench;

public sealed class ExternalBenchRunner(ExternalBenchOptions options, ProcessTimer timer, ILogger<ExternalBenchRunner> logger)
{
    const int TIMED_RUNS = 5;
    const int RG_SUCCESS = 0;
    // the fetchle query matches nothing on purpose, so a full walk exits "no results"
    const int FETCHLE_NO_RESULTS = 1;
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
            string[] fetchleArgs = ["zzz", "--root", root, "--budget", "5m", "--plain"];
            commands.Add(new ToolCommand("fetchle naive, full walk", fetchleExe, fetchleArgs, FETCHLE_NO_RESULTS));
        }
        if (rgExe is not null)
        {
            string[] rgArgs = ["--files", "--hidden", "--no-ignore", root];
            string[] rgSingleThreadArgs = ["-j1", "--files", "--hidden", "--no-ignore", root];
            commands.Add(new ToolCommand("rg --files", rgExe, rgArgs, RG_SUCCESS));
            commands.Add(new ToolCommand("rg -j1 --files", rgExe, rgSingleThreadArgs, RG_SUCCESS));
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

        var result = new ExternalResult(command.Tool, shape, Median(runsMs), runsMs);
        logger.MeasuredTool(result.Tool, result.Shape, result.MedianMs, result.RunsMs);
        return result;
    }

    static long Median(long[] values)
    {
        var sorted = (long[])values.Clone();
        Array.Sort(sorted);
        return sorted[sorted.Length / 2];
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
