using Microsoft.Extensions.Logging;

namespace Fetchle.Bench;

static partial class BenchLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "wrote {Path}")]
    public static partial void WroteResults(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "skipping fetchle: no exe at {Path}, run dotnet build.cs publish or set FETCHLE_EXE")]
    public static partial void SkippingFetchle(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "skipping rg: not on PATH")]
    public static partial void SkippingRg(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "timing {Tool} on {Shape}")]
    public static partial void TimingTool(this ILogger logger, string tool, string shape);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Tool} on {Shape}: p50 {P50Ms} ms, p95 {P95Ms} ms, runs {RunsMs}")]
    public static partial void MeasuredTool(this ILogger logger, string tool, string shape, long p50Ms, long p95Ms, long[] runsMs);
}
