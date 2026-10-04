using Microsoft.Extensions.Logging;

namespace Fetchle.Bench;

static partial class BenchLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "wrote {Path}")]
    public static partial void WroteResults(this ILogger logger, string path);
}
