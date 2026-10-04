using System.Diagnostics;

namespace Fetchle.Core.Search;

// a query's time budget. checked per entry rather than via a timer, so a zero budget
// stops on the first entry instead of racing a timer thread
public readonly struct Deadline
{
    readonly long _end;

    Deadline(long end) => _end = end;

    public static Deadline After(TimeSpan budget, long startTimestamp)
    {
        // in doubles first: a long budget times a high Stopwatch.Frequency overflows long
        var ticks = budget.TotalSeconds * Stopwatch.Frequency;
        return new(ticks >= long.MaxValue - startTimestamp ? long.MaxValue : startTimestamp + (long)ticks);
    }

    public bool IsExpired(long nowTimestamp) => nowTimestamp >= _end;

    public bool IsExpired() => IsExpired(Stopwatch.GetTimestamp());
}
