using Microsoft.Extensions.Logging;

namespace Fetchle.Bench;

// one warmup, then TimedRuns timed runs, p50 and p95 by nearest rank. a tool that goes over the time guard
// stops there and the result says what ran, so the job stays bounded
public sealed class ToolMeasurer(ProcessTimer timer, ILogger<ToolMeasurer> logger, TimeSpan? maxSingleRun = null)
{
    // p95 needs more than a handful of runs to mean anything
    public const int TIMED_RUNS = 20;
    const int P50 = 50;
    const int P95 = 95;
    const int RESULT_DECIMALS = 1;
    const int MAX_REASON_CHARS = 600;

    public static readonly TimeSpan DefaultMaxSingleRun = TimeSpan.FromSeconds(60);
    static readonly TimeSpan HardTimeout = TimeSpan.FromMinutes(5);
    static readonly TimeSpan SetupTimeout = TimeSpan.FromMinutes(30);

    readonly TimeSpan _maxSingleRun = maxSingleRun ?? DefaultMaxSingleRun;

    public ExternalResult Measure(ToolPlan plan, string shape, string root)
    {
        var command = plan.Timed;
        var shown = Display(command, root);
        logger.TimingTool(command.Tool, shape);
        try
        {
            foreach (var step in plan.Setup)
            {
                timer.TimeRunMs(step, SetupTimeout);
            }
            return Run(command, shape, shown);
        }
        catch (TimeoutException e)
        {
            logger.ToolStopped(command.Tool, shape, e.Message);
            return Outcome(command, shape, shown, ResultStatus.SKIPPED, e.Message, []);
        }
        catch (InvalidOperationException e)
        {
            // a crashed child dumps a whole stack trace into the message
            var reason = e.Message.Length > MAX_REASON_CHARS ? e.Message[..MAX_REASON_CHARS] + "..." : e.Message;
            logger.ToolFailed(command.Tool, shape, reason);
            return Outcome(command, shape, shown, ResultStatus.FAILED, reason, []);
        }
    }

    ExternalResult Run(ToolCommand command, string shape, string shown)
    {
        // the untimed warmup also tells us whether the tool is even worth repeating
        var warmup = timer.TimeRunMs(command, HardTimeout);
        if (warmup > _maxSingleRun.TotalMilliseconds)
        {
            var reason = $"warmup took {warmup:0} ms, over the {_maxSingleRun.TotalSeconds:0} s guard, so no timed runs";
            logger.ToolStopped(command.Tool, shape, reason);
            return Outcome(command, shape, shown, ResultStatus.SKIPPED, reason, [warmup]);
        }

        var runs = new List<double>(TIMED_RUNS);
        string? stoppedBecause = null;
        for (var i = 0; i < TIMED_RUNS && stoppedBecause is null; i++)
        {
            try
            {
                runs.Add(timer.TimeRunMs(command, HardTimeout));
            }
            catch (TimeoutException e)
            {
                stoppedBecause = e.Message;
                break;
            }
            if (runs[^1] > _maxSingleRun.TotalMilliseconds)
            {
                stoppedBecause = $"run {i + 1} took {runs[^1]:0} ms, over the {_maxSingleRun.TotalSeconds:0} s guard";
            }
        }

        if (stoppedBecause is not null)
        {
            logger.ToolStopped(command.Tool, shape, $"{stoppedBecause}, kept {runs.Count} of {TIMED_RUNS} runs");
        }
        var status = stoppedBecause is null ? ResultStatus.OK : runs.Count == 0 ? ResultStatus.SKIPPED : ResultStatus.PARTIAL;
        var result = Outcome(command, shape, shown, status, stoppedBecause, [.. runs]);
        if (result.P50Ms is not null)
        {
            logger.MeasuredTool(command.Tool, shape, result.P50Ms.Value, result.P95Ms!.Value);
        }
        return result;
    }

    static ExternalResult Outcome(ToolCommand command, string shape, string shown, string status, string? reason, double[] runs)
    {
        var rounded = runs.Select(r => Math.Round(r, RESULT_DECIMALS)).ToArray();
        // a skipped tool that only got its warmup in has no timed runs to rank
        var ranked = status is ResultStatus.OK or ResultStatus.PARTIAL && rounded.Length > 0;
        return new ExternalResult(
            command.Tool,
            shape,
            status,
            reason,
            shown,
            ranked ? Percentiles.NearestRank(rounded, P50) : null,
            ranked ? Percentiles.NearestRank(rounded, P95) : null,
            rounded);
    }

    // the corpus root and the machine's tool paths would only make the committed json noisy
    public static string Display(ToolCommand command, string root)
    {
        var exe = Path.GetFileName(command.Executable);
        return $"{exe} {string.Join(' ', command.Arguments)}".Replace(root, "{corpus}");
    }
}
