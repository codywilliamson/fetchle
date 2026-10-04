using Fetchle.Cli.Commands;

namespace Fetchle.Tests.Cli;

public class SinceCutoffTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Subtracts_the_window() =>
        await Assert.That(SearchArgs.SinceCutoff(TimeSpan.FromDays(3), Now)).IsEqualTo(Now.AddDays(-3));

    [Test]
    public async Task Clamps_windows_older_than_time_itself() =>
        await Assert.That(SearchArgs.SinceCutoff(TimeSpan.FromDays(1_000_000), Now)).IsEqualTo(DateTimeOffset.MinValue);
}
