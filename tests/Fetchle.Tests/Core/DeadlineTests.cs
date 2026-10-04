using System.Diagnostics;
using Fetchle.Core.Search;

namespace Fetchle.Tests.Core;

public class DeadlineTests
{
    [Test]
    public async Task Zero_budget_is_expired_at_start()
    {
        var start = Stopwatch.GetTimestamp();
        await Assert.That(Deadline.After(TimeSpan.Zero, start).IsExpired(start)).IsTrue();
    }

    [Test]
    public async Task Expires_exactly_at_the_budget()
    {
        var start = 1_000L;
        var deadline = Deadline.After(TimeSpan.FromSeconds(1), start);
        await Assert.That(deadline.IsExpired(start + Stopwatch.Frequency - 1)).IsFalse();
        await Assert.That(deadline.IsExpired(start + Stopwatch.Frequency)).IsTrue();
    }

    [Test]
    public async Task Huge_budget_never_overflows() =>
        await Assert.That(Deadline.After(TimeSpan.MaxValue, Stopwatch.GetTimestamp()).IsExpired(long.MaxValue - 1)).IsFalse();
}
