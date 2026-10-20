using Microsoft.Extensions.Logging;

namespace Fetchle.Bench;

static partial class BenchLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "wrote {Path}")]
    public static partial void WroteResults(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "skipping {Tool}: {Reason}")]
    public static partial void SkippingTool(this ILogger logger, string tool, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "timing {Tool} on {Shape}")]
    public static partial void TimingTool(this ILogger logger, string tool, string shape);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Tool} on {Shape}: p50 {P50Ms} ms, p95 {P95Ms} ms")]
    public static partial void MeasuredTool(this ILogger logger, string tool, string shape, double p50Ms, double p95Ms);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Tool} on {Shape} stopped: {Reason}")]
    public static partial void ToolStopped(this ILogger logger, string tool, string shape, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Tool} on {Shape} FAILED: {Reason}")]
    public static partial void ToolFailed(this ILogger logger, string tool, string shape, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "corpus {Shape} ready in {ElapsedMs} ms: {Entries} entries on disk, {WalkerEntries} seen by the walker")]
    public static partial void CorpusReady(this ILogger logger, string shape, long elapsedMs, int entries, int walkerEntries);
}
