using Fetchle.Core.Index;
using Fetchle.Core.Ranking;

namespace Fetchle.Tests.Core.Ranking;

public class BfsLevelsTests
{
    [Test]
    public async Task Levels_cover_every_row_and_each_row_sits_one_level_below_its_parent()
    {
        var root = Path.Join(Path.GetTempPath(), "fetchle-levels-never-created");
        var tree = TreeFromPaths.Build(root, Fetchle.Fixtures.HomeCorpus.Entries(20_000).Take(20_000));
        using var snapshot = Snapshot.FromBytes(SnapshotWriter.ToBytes(tree, DateTimeOffset.UnixEpoch));
        var parents = snapshot.Parents.ToArray();
        var levels = BfsLevels.Of(parents, snapshot.RootCount);

        var levelOf = new int[parents.Length];
        var expectedStart = snapshot.RootCount;
        for (var l = 0; l < levels.Count; l++)
        {
            var (start, end) = levels[l];
            await Assert.That(start).IsEqualTo(expectedStart);
            for (var i = start; i < end; i++)
            {
                levelOf[i] = l + 1;
            }
            expectedStart = end;
        }
        await Assert.That(expectedStart).IsEqualTo(parents.Length);

        var misplaced = 0;
        for (var i = snapshot.RootCount; i < parents.Length; i++)
        {
            if (levelOf[i] != levelOf[parents[i]] + 1)
            {
                misplaced++;
            }
        }
        await Assert.That(misplaced).IsEqualTo(0);
        await Assert.That(levels.Count).IsGreaterThan(3);
    }

    [Test]
    public async Task A_snapshot_with_only_roots_has_no_levels()
    {
        var levels = BfsLevels.Of([uint.MaxValue], roots: 1);
        await Assert.That(levels).IsEmpty();
    }
}
