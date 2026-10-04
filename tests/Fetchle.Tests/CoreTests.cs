using System.Diagnostics;
using Fetchle.Core;

namespace Fetchle.Tests;

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

public class SubstringRankerTests
{
    [Test]
    public async Task Name_match_beats_path_match()
    {
        var ranker = new SubstringRanker("settings");
        await Assert.That(ranker.Score("app/Settings.json", "Settings.json")).IsEqualTo(SubstringRanker.NameMatch);
        await Assert.That(ranker.Score("settings/app.json", "app.json")).IsEqualTo(SubstringRanker.PathMatch);
        await Assert.That(ranker.Score("app/main.cs", "main.cs")).IsEqualTo(0d);
    }

    [Test]
    public async Task Matches_across_separators() =>
        await Assert.That(new SubstringRanker("user/settings").Score("Code/User/settings.json", "settings.json")).IsEqualTo(SubstringRanker.PathMatch);
}

public class PruneRulesTests
{
    [Test]
    [Arguments("node_modules")]
    [Arguments(".git")]
    public async Task Prunes_default_names(string name) =>
        await Assert.That(PruneRules.Default.ShouldPrune("/any/where", name)).IsTrue();

    [Test]
    public async Task Prunes_full_paths_by_parent_and_name()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fetchle-prune"));
        var rules = new PruneRules([], [Path.Combine(root, "cache")]);
        await Assert.That(rules.ShouldPrune(root, "cache")).IsTrue();
        await Assert.That(rules.ShouldPrune(root + Path.DirectorySeparatorChar, "cache")).IsTrue();
        await Assert.That(rules.ShouldPrune(Path.Combine(root, "other"), "cache")).IsFalse();
        await Assert.That(rules.ShouldPrune(root, "src")).IsFalse();
    }
}

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
