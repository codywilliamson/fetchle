using Fetchle.Core.Index;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;
using Fetchle.Fixtures;
using Fetchle.Tests.Core.Ranking;

namespace Fetchle.Tests.Core.Index;

public class VectorRebuildTests
{
    [Test]
    public async Task Rebuild_with_a_model_writes_vectors_for_the_new_snapshot()
    {
        using var tree = FixtureTree.Create(fillerFiles: 100);
        using var index = new TempIndex();
        var report = index.Store.Rebuild([tree.Root], PruneRules.Default, new FakeEncoder(), CancellationToken.None);

        await Assert.That(report.EmbedTime).IsNotNull();
        await Assert.That(File.Exists(index.Store.VectorsPath)).IsTrue();
        var status = new IndexedFileSearch(PruneRules.Default, index.Store, new FakeEncoder()).GetStatus([tree.Root]);
        await Assert.That(status.VectorsComplete).IsTrue();
    }

    [Test]
    public async Task Without_a_model_there_are_no_vectors_and_search_still_answers()
    {
        using var tree = FixtureTree.Create(fillerFiles: 100);
        using var index = TempIndex.Built(tree.Root);

        await Assert.That(File.Exists(index.Store.VectorsPath)).IsFalse();
        var result = new IndexedFileSearch(PruneRules.Default, index.Store, new FakeEncoder()).Search(
            new SearchRequest("settings", [tree.Root], 5, TimeSpan.FromSeconds(10)), CancellationToken.None);
        await Assert.That(result.Hits.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Vectors_left_from_the_previous_snapshot_turn_semantic_off()
    {
        using var tree = FixtureTree.Create(fillerFiles: 100);
        using var index = new TempIndex();
        index.Store.Rebuild([tree.Root], PruneRules.Default, new FakeEncoder(), CancellationToken.None);
        File.WriteAllText(tree.Full("src/app/new-file.txt"), "");
        // a rebuild without the model swaps the snapshot and leaves the old vectors behind
        index.Store.Rebuild([tree.Root], PruneRules.Default, CancellationToken.None);

        using var session = SearchSession.Open(index.Store, PruneRules.Default, new FakeEncoder());
        await Assert.That(session.SemanticOff).IsEqualTo("vectors are for another snapshot");
    }
}
