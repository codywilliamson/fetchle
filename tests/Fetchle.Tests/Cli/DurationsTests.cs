using Fetchle.Cli;
using Fetchle.Cli.Commands;

namespace Fetchle.Tests.Cli;

public class DurationsTests
{
    [Test]
    [Arguments("250ms", 250)]
    [Arguments("2s", 2_000)]
    [Arguments("1.5s", 1_500)]
    [Arguments("5m", 300_000)]
    [Arguments("1h", 3_600_000)]
    [Arguments("3d", 259_200_000)]
    [Arguments(" 2s ", 2_000)]
    public async Task Parses_units(string text, long expectedMs)
    {
        await Assert.That(Durations.TryParse(text, out var d)).IsTrue();
        await Assert.That((long)d.TotalMilliseconds).IsEqualTo(expectedMs);
    }

    [Test]
    [Arguments("")]
    [Arguments("2")]
    [Arguments("s")]
    [Arguments("2x")]
    [Arguments("-2s")]
    [Arguments("2 s")]
    [Arguments("1.2.3s")]
    public async Task Rejects_garbage(string text) =>
        await Assert.That(Durations.TryParse(text, out _)).IsFalse();
}
