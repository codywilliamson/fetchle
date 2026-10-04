using Microsoft.Extensions.Logging;

namespace Fetchle.Evals.Logging;

public static partial class EvalsLog
{
    public static ILoggerFactory CreateFactory() => LoggerFactory.Create(builder =>
    {
        // stdout carries the eval table, so log lines go to stderr
        builder.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
    });

    [LoggerMessage(Level = LogLevel.Information, Message = "wrote {Path}")]
    public static partial void WroteResults(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "updated {Path}")]
    public static partial void UpdatedBaseline(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "ranking regressed: top-1 {Top1:P0} top-3 {Top3:P0}, baseline top-1 {BaselineTop1:P0} top-3 {BaselineTop3:P0}")]
    public static partial void RankingRegressed(this ILogger logger, double top1, double top3, double baselineTop1, double baselineTop3);
}
