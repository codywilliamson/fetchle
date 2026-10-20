using System.Text.Json.Serialization;

namespace Fetchle.Bench;

public static class ResultStatus
{
    public const string OK = "ok";

    // some timed runs finished before a run went over the time guard
    public const string PARTIAL = "partial";
    public const string SKIPPED = "skipped";
    public const string FAILED = "failed";
}

// walker counts use the default prune rules, the plain ones count everything on disk
public sealed record CorpusCounts(string Shape, int Dirs, int Files, int Entries, int WalkerDirs, int WalkerFiles, int WalkerEntries);

public sealed record SkippedTool(string Tool, string Reason);

public sealed record ExternalResult(string Tool, string Shape, string Status, string? Reason, string Command, double? P50Ms, double? P95Ms, double[] RunsMs);

public sealed record ExternalReport(
    string Machine,
    string Os,
    string OsFamily,
    int CpuCount,
    string Query,
    int TimedRuns,
    List<CorpusCounts> Corpora,
    List<SkippedTool> SkippedTools,
    List<ExternalResult> Results);

[JsonSerializable(typeof(ExternalReport))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = true)]
public partial class ExternalJson : JsonSerializerContext;
