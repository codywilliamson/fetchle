using Fetchle.Fixtures;

namespace Fetchle.Bench.Tests;

public class HomeCorpusTests
{
    const int SMALL = 30_000;
    const int PLAN_SIZE = 200_000;

    [Test]
    public async Task Plan_hits_the_entry_target_exactly()
    {
        var stats = HomeCorpus.Describe(SMALL);
        await Assert.That(stats.Entries).IsEqualTo(SMALL);
    }

    [Test]
    public async Task Same_seed_gives_the_same_corpus_and_another_seed_a_different_one()
    {
        await Assert.That(HomeCorpus.Describe(SMALL, seed: 1)).IsEqualTo(HomeCorpus.Describe(SMALL, seed: 1));
        await Assert.That(HomeCorpus.Describe(SMALL, seed: 1)).IsNotEqualTo(HomeCorpus.Describe(SMALL, seed: 2));
    }

    [Test]
    public async Task A_real_share_of_entries_sit_under_pruned_dirs()
    {
        var share = HomeCorpus.Describe(PLAN_SIZE).PrunedShare;
        await Assert.That(share).IsBetween(0.25, 0.50);
    }

    [Test]
    public async Task Files_per_dir_are_heavy_tailed()
    {
        var perDir = HomePlanner.Build(PLAN_SIZE, Corpus.DEFAULT_SEED).GeneratedFiles.Where(n => n > 0).Order().ToArray();
        await Assert.That(perDir[perDir.Length / 2]).IsLessThanOrEqualTo(8);
        await Assert.That(perDir[^1]).IsGreaterThanOrEqualTo(500);
    }

    [Test]
    public async Task Most_dirs_sit_at_depth_3_to_10_with_a_deeper_tail()
    {
        var depths = HomePlanner.Build(PLAN_SIZE, Corpus.DEFAULT_SEED).Depths.Skip(1).ToArray();
        var inBand = depths.Count(d => d is >= 3 and <= 10);
        await Assert.That((double)inBand / depths.Length).IsGreaterThan(0.8);
        await Assert.That(depths.Max()).IsGreaterThan(10);
    }

    [Test]
    public async Task Disk_holds_exactly_the_planned_entries_and_the_landmarks()
    {
        using var scratch = new TempDir();
        var root = Path.Combine(scratch.Path, "home");

        var stats = HomeCorpus.Ensure(root, SMALL);

        var onDisk = Directory.EnumerateFileSystemEntries(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }).Count();
        await Assert.That(onDisk).IsEqualTo(stats.Entries);
        foreach (var landmark in Corpus.Landmarks)
        {
            await Assert.That(File.Exists(Path.Combine(root, landmark))).IsTrue();
        }
    }

    [Test]
    public async Task Second_ensure_reuses_the_cached_corpus()
    {
        using var scratch = new TempDir();
        var root = Path.Combine(scratch.Path, "home");
        var first = HomeCorpus.Ensure(root, SMALL);
        var stray = Path.Combine(root, "stray.txt");
        File.WriteAllText(stray, "");

        var second = HomeCorpus.Ensure(root, SMALL);

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(File.Exists(stray)).IsTrue();
    }
}
