using Fetchle.Cli;

namespace Fetchle.Tests;

public class HumanizeTests
{
    [Test]
    [Arguments(0L, "0 B")]
    [Arguments(512L, "512 B")]
    [Arguments(1_536L, "1.5 KB")]
    [Arguments(222_298_112L, "212 MB")]
    [Arguments(1_677_721L, "1.6 MB")]
    public async Task Size(long bytes, string expected) =>
        await Assert.That(Humanize.Size(bytes)).IsEqualTo(expected);

    [Test]
    [Arguments(10d, "just now")]
    [Arguments(300d, "5m ago")]
    [Arguments(7_200d, "2h ago")]
    [Arguments(172_800d, "2d ago")]
    [Arguments(63_072_000d, "2y ago")]
    public async Task Age(double seconds, string expected) =>
        await Assert.That(Humanize.Age(TimeSpan.FromSeconds(seconds))).IsEqualTo(expected);
}
