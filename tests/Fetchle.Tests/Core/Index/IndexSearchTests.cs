using Fetchle.Core.Index;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;
using Fetchle.Fixtures;

namespace Fetchle.Tests.Core.Index;

// the index must answer exactly what a live walk of the same tree answers: same total, same hits, same order
public class IndexSearchTests
{
    static readonly string Sep = Path.DirectorySeparatorChar.ToString();

    public static IEnumerable<string> Queries() =>
    [
        "settings", "SETTINGS", "e", ".json", "json", "main.cs", "café", "CAFÉ", "日本", "🦊", "fox notes",
        "zzz-nothing", "deep-target", "readme-collide", "filler", $"src{Sep}app", $"app{Sep}settings", "d" + Sep, "",
    ];

    [Test]
    [MethodDataSource(nameof(Queries))]
    public async Task Index_matches_a_live_walk(string query)
    {
        using var tree = FixtureTree.Create(fillerFiles: 2000);
        using var index = TempIndex.Built(tree.Root);
        foreach (var request in Requests(query, tree.Root))
        {
            await AssertSame(index, request);
        }
    }

    [Test]
    public async Task A_subdir_root_is_answered_from_the_index_relative_to_itself()
    {
        using var tree = FixtureTree.Create(fillerFiles: 500);
        using var index = TempIndex.Built(tree.Root);
        foreach (var query in (string[])["app", "settings", "filler", "e"])
        {
            await AssertSame(index, Request(query, [tree.Full("src")], 50));
            await AssertSame(index, Request(query, [tree.Full("filler")], 50));
        }
    }

    [Test]
    public async Task Several_roots_and_unindexed_roots_merge_like_a_walk()
    {
        using var a = FixtureTree.Create(seed: 1, fillerFiles: 300);
        using var b = FixtureTree.Create(seed: 2, fillerFiles: 300);
        using var index = TempIndex.Built(a.Root);
        // b isn't in the snapshot, so it's walked live and merged in
        await AssertSame(index, Request("e", [a.Root, b.Root], 40));
        await AssertSame(index, Request("settings", [b.Root, a.Root], 40));
    }

    [Test]
    public async Task Several_indexed_roots_with_subdirs_build_and_search_like_a_walk()
    {
        using var a = FixtureTree.Create(seed: 1, fillerFiles: 300);
        using var b = FixtureTree.Create(seed: 2, fillerFiles: 300);
        using var c = FixtureTree.Create(seed: 3, fillerFiles: 300);
        using var index = TempIndex.Built(a.Root, b.Root, c.Root);
        var info = index.Store.ReadInfo()!;

        await Assert.That(info.Roots.Count).IsEqualTo(3);
        await Assert.That(info.Roots.Sum(r => r.Entries) + 3).IsEqualTo(info.Entries);
        await AssertSame(index, Request("e", [a.Root, b.Root, c.Root], 60));
        await AssertSame(index, Request("settings", [c.Root], 10));
        await AssertSame(index, Request("filler", [b.Full("filler")], 10));
    }

    [Test]
    public async Task Search_reads_the_snapshot_not_the_disk()
    {
        using var tree = FixtureTree.Create(fillerFiles: 50);
        using var index = TempIndex.Built(tree.Root);
        File.WriteAllText(tree.Full("src/app/added-after-index.txt"), "");

        var result = new IndexedFileSearch(PruneRules.Default, index.Store).Search(Request("added-after-index", [tree.Root], 10), CancellationToken.None);

        await Assert.That(result.TotalMatches).IsEqualTo(0);
    }

    [Test]
    public async Task Spent_budget_stops_early()
    {
        using var tree = FixtureTree.Create(fillerFiles: 50);
        using var index = TempIndex.Built(tree.Root);

        var result = new IndexedFileSearch(PruneRules.Default, index.Store).Search(Request("e", [tree.Root], 5) with { Budget = TimeSpan.Zero }, CancellationToken.None);

        await Assert.That(result.StoppedEarly).IsEqualTo(StopReasons.BUDGET);
    }

    [Test]
    public async Task Rebuild_places_every_walked_entry()
    {
        using var tree = FixtureTree.Create(fillerFiles: 2000);
        var walk = IndexStore.WalkRoot(tree.Root, PruneRules.Default);
        var tree2 = TreeAssembler.Assemble([walk]);
        var walked = WalkHelpers.NaiveWalk(tree.Root).Count();

        await Assert.That(tree2.UnattachedDirs).IsEqualTo(0);
        await Assert.That(tree2.EntryCount).IsEqualTo(walked + 1);
        await Assert.That(tree2.Roots[0].Entries).IsEqualTo(walked);
    }

    static IEnumerable<SearchRequest> Requests(string query, string root)
    {
        yield return Request(query, [root], SearchRequest.DEFAULT_LIMIT);
        yield return Request(query, [root], 1);
        yield return Request(query, [root], 5000);
        yield return Request(query, [root], 25) with { Type = EntryType.Files };
        yield return Request(query, [root], 25) with { Type = EntryType.Directories };
        yield return Request(query, [root], 25) with { Extensions = ["json", ".md"] };
        yield return Request(query, [root], 25) with { ModifiedSince = DateTimeOffset.UtcNow.AddDays(-1) };
    }

    static SearchRequest Request(string query, IReadOnlyList<string> roots, int limit) => new(query, roots, limit, TimeSpan.FromMinutes(5));

    static async Task AssertSame(TempIndex index, SearchRequest request)
    {
        var live = new FileSearch(PruneRules.Default).Search(request, CancellationToken.None);
        var indexed = new IndexedFileSearch(PruneRules.Default, index.Store).Search(request, CancellationToken.None);

        await Assert.That(indexed.TotalMatches).IsEqualTo(live.TotalMatches);
        await Assert.That(string.Join('\n', indexed.Hits.Select(h => $"{h.Score} {h.Path}"))).IsEqualTo(string.Join('\n', live.Hits.Select(h => $"{h.Score} {h.Path}")));
        await Assert.That(indexed.StoppedEarly).IsNull();
    }
}
