using Fetchle.Core.Search;

namespace Fetchle.Tests.Core;

public class FooterTests
{
    static readonly SearchHit Hit = new("/a", 1, 1, DateTimeOffset.UnixEpoch);

    [Test]
    public async Task Complete() =>
        await Assert.That(new SearchResult([Hit], 312, TimeSpan.FromMilliseconds(4.7), null).Footer()).IsEqualTo("312 matches, showing 1, 4ms");

    [Test]
    public async Task Stopped_early() =>
        await Assert.That(new SearchResult([], 0, TimeSpan.FromMilliseconds(2001), StopReasons.Budget).Footer()).IsEqualTo("0 matches, showing 0, 2001ms, stopped early: budget");
}
