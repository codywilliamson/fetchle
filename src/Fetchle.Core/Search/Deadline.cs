using System.Diagnostics;

namespace Fetchle.Core.Search;

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
