using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Fetchle.Bench;

// times the same query on fetchle and every walker we compare against, on each corpus shape,
// and writes one json file for this os
public sealed class ExternalBenchRunner(ExternalBenchOptions options, ToolMeasurer measurer, ILogger<ExternalBenchRunner> logger)
{
    const int EXIT_OK = 0;
    const int EXIT_FAILED_TOOLS = 1;
    const int EXIT_NO_FETCHLE = 2;
    static readonly string SNAPSHOT_PATH = Path.Combine("index", "snapshot.fidx");

    public int Run()
    {
        var skipped = new List<SkippedTool>();
        var tools = LocatedTools.Locate(options, skipped, logger);

        // rg or fd missing just narrows the comparison, but a run without fetchle proves nothing
        if (tools.Fetchle is null)
        {
            return EXIT_NO_FETCHLE;
        }

        var corpora = new List<CorpusCounts>();
        var results = new List<ExternalResult>();
        foreach (var plan in ToolPlans.Baselines(tools))
        {
            results.Add(measurer.Measure(plan, BenchCorpus.NONE, ""));
        }

        foreach (var shape in options.Shapes)
        {
            var started = Stopwatch.GetTimestamp();
            var root = BenchCorpus.Ensure(shape);
            var counts = CorpusCounter.Count(shape, root);
            logger.CorpusReady(shape, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, counts.Entries, counts.WalkerEntries);
            corpora.Add(counts);

            using var indexDirs = new IndexDirsScope();
            foreach (var plan in ToolPlans.For(tools, root, indexDirs.Dirs))
            {
                results.Add(measurer.Measure(plan, shape, root));
            }

            var snapshot = new FileInfo(Path.Combine(indexDirs.Dirs.Query, SNAPSHOT_PATH));
            corpora[^1] = counts with { SnapshotBytes = snapshot.Exists ? snapshot.Length : null };
        }

        WriteReport(corpora, skipped, results);
        // a comparison tool that can't cope with a shape is recorded as failed in the json; only fetchle failing fails the run
        return results.Any(r => r.Status == ResultStatus.FAILED && r.Tool.StartsWith("fetchle", StringComparison.Ordinal)) ? EXIT_FAILED_TOOLS : EXIT_OK;
    }

    void WriteReport(List<CorpusCounts> corpora, List<SkippedTool> skipped, List<ExternalResult> results)
    {
        var report = new ExternalReport(
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            ExternalBenchOptions.OsFamily,
            Environment.ProcessorCount,
            ToolPlans.QUERY,
            ToolMeasurer.TIMED_RUNS,
            corpora,
            skipped,
            results);
        var path = Path.Combine(options.ResultsDirectory, ExternalBenchOptions.ResultsFileName);
        File.WriteAllText(path, JsonSerializer.Serialize(report, ExternalJson.Default.ExternalReport) + "\n");
        logger.WroteResults(path);
    }
}
