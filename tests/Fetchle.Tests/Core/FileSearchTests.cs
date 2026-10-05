using Fetchle.Core.Search;
using Fetchle.Core.Walking;
using Fetchle.Fixtures;

namespace Fetchle.Tests.Core;

public class FileSearchTests
{
    [Test]
    public async Task Bad_root_syntax_is_an_invalid_root_not_a_crash()
    {
        var search = new FileSearch(PruneRules.Default);
        var request = new SearchRequest("x", ["bad\0root"], SearchRequest.DEFAULT_LIMIT, SearchRequest.DefaultBudget);

        var e = await Assert.That(() => search.Search(request, CancellationToken.None)).Throws<InvalidRootException>();
        await Assert.That(e!.Message).StartsWith("invalid root path");
    }

    [Test]
    public async Task Same_search_gives_identical_output_every_run()
    {
        using var tree = FixtureTree.Create(fillerFiles: 2000);
        var search = new FileSearch(PruneRules.Default);
        // "e" matches most entries, so top-k is full of ties and merge order would show
        var request = new SearchRequest("e", [tree.Root], 25, SearchRequest.DefaultBudget);
        var expectedTotal = WalkHelpers.NaiveWalk(tree.Root)
            .Count(p => Path.GetRelativePath(tree.Root, p).Contains('e', StringComparison.OrdinalIgnoreCase));

        var first = search.Search(request, CancellationToken.None);
        await Assert.That(first.TotalMatches).IsEqualTo(expectedTotal);
        await Assert.That(first.Hits.Count).IsEqualTo(25);

        var firstPaths = string.Join(';', first.Hits.Select(h => h.Path));
        for (var run = 0; run < 50; run++)
        {
            var again = search.Search(request, CancellationToken.None);
            await Assert.That(again.TotalMatches).IsEqualTo(first.TotalMatches);
            await Assert.That(string.Join(';', again.Hits.Select(h => h.Path))).IsEqualTo(firstPaths);
        }
    }

    [Test]
    public async Task Spent_budget_stops_early_with_the_budget_reason()
    {
        using var tree = FixtureTree.Create(fillerFiles: 50);
        var request = new SearchRequest("e", [tree.Root], 5, TimeSpan.Zero);

        var result = new FileSearch(PruneRules.Default).Search(request, CancellationToken.None);

        await Assert.That(result.StoppedEarly).IsEqualTo(StopReasons.BUDGET);
    }
}
