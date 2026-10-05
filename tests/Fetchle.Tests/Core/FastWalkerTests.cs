using Fetchle.Core.Search;
using Fetchle.Core.Walking;
using Fetchle.Fixtures;
using static Fetchle.Tests.Core.WalkHelpers;

namespace Fetchle.Tests.Core;

public class FastWalkerTests
{
    static bool Completes(string lister, string root) => FastWalkAll(lister, root).Completed;

    static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fetchle-walker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task Yields_every_file_in_a_nested_tree(string lister)
    {
        using var tree = FixtureTree.Create(fillerFiles: 50);
        var seen = FastWalk(lister, tree.Root);

        foreach (var file in (string[])[FixtureTree.SETTINGS_FILE, "src/app/main.cs", "docs/readme.md", .. FixtureTree.UnicodeFiles, .. tree.FillerFiles])
        {
            await Assert.That(seen.Contains(tree.Full(file))).IsTrue();
        }
    }

    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task Prunes_default_pruned_dirs_without_descending(string lister)
    {
        using var tree = FixtureTree.Create(fillerFiles: 10);
        var seen = FastWalk(lister, tree.Root);

        await Assert.That(seen.Contains(tree.Full("node_modules"))).IsFalse();
        await Assert.That(seen.Contains(tree.Full(FixtureTree.PRUNED_SETTINGS_FILE))).IsFalse();
        await Assert.That(seen.Contains(tree.Full(".git"))).IsFalse();
        await Assert.That(seen.Contains(tree.Full(FixtureTree.GIT_SETTINGS_FILE))).IsFalse();
    }

    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task Skips_reparse_points_and_ends_on_a_symlink_cycle(string lister)
    {
        using var tree = FixtureTree.Create(fillerFiles: 10);
        Skip.Unless(tree.Created.HasFlag(FixtureFeatures.SymlinkLoop), "can't create links here");

        var seen = FastWalk(lister, tree.Root);

        await Assert.That(seen.Any(p => p.StartsWith(tree.Full(FixtureTree.LOOP_DIR), PathComparison.Default) && p.Length > tree.Full(FixtureTree.LOOP_DIR).Length)).IsFalse();
        await Assert.That(seen.Any(p => p.StartsWith(tree.Full(FixtureTree.JUNCTION_DIR), PathComparison.Default) && p.Length > tree.Full(FixtureTree.JUNCTION_DIR).Length)).IsFalse();
        await Assert.That(seen.Contains(tree.Full(FixtureTree.SETTINGS_FILE))).IsTrue();
    }

    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task Walks_a_tree_deeper_than_260_chars(string lister)
    {
        using var tree = FixtureTree.Create(fillerFiles: 10);

        await Assert.That(FastWalk(lister, tree.Root).Contains(tree.Full(tree.LongPathFile))).IsTrue();
    }

    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task Tolerates_an_inaccessible_directory(string lister)
    {
        using var tree = FixtureTree.Create(fillerFiles: 10);
        Skip.Unless(tree.Created.HasFlag(FixtureFeatures.Unreadable), "this user can read everything");

        var seen = FastWalk(lister, tree.Root);

        await Assert.That(seen.Contains(tree.Full(FixtureTree.LOCKED_FILE))).IsFalse();
        await Assert.That(seen.Contains(tree.Full(FixtureTree.SETTINGS_FILE))).IsTrue();
    }

    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task Empty_root_visits_nothing(string lister)
    {
        var root = TempDir();
        try
        {
            await Assert.That(FastWalk(lister, root).Count).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(root);
        }
    }

    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task Missing_root_visits_nothing_and_does_not_throw(string lister)
    {
        var root = Path.Combine(Path.GetTempPath(), $"fetchle-missing-{Guid.NewGuid():N}");
        await Assert.That(FastWalk(lister, root).Count).IsEqualTo(0);
        await Assert.That(Completes(lister, root)).IsTrue();
    }

    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task File_root_visits_nothing_and_does_not_throw(string lister)
    {
        var root = TempDir();
        try
        {
            var file = Path.Combine(root, "a.txt");
            File.WriteAllText(file, "x");
            await Assert.That(FastWalk(lister, file).Count).IsEqualTo(0);
            await Assert.That(Completes(lister, file)).IsTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task Expired_deadline_reports_a_cut_short_walk(string lister)
    {
        using var tree = FixtureTree.Create(fillerFiles: 10);
        var expired = Deadline.After(TimeSpan.Zero, 0);

        var completed = Walker(lister).Walk(tree.Root, () => (ref _) => { }, expired);

        await Assert.That(completed).IsFalse();
    }

    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task Matches_the_naive_walker_on_the_same_fixture(string lister)
    {
        using var tree = FixtureTree.Create(fillerFiles: 200);

        await Assert.That(FastWalk(lister, tree.Root).OrderBy(p => p, StringComparer.Ordinal))
            .IsEquivalentTo(NaiveWalk(tree.Root).OrderBy(p => p, StringComparer.Ordinal));
    }

    // races show up randomly, so repeat the parity check until a lost or doubled entry is loud
    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task Matches_the_naive_walker_on_every_one_of_many_runs(string lister)
    {
        const int RUNS = 20;
        using var tree = FixtureTree.Create(fillerFiles: 500);
        var expected = NaiveWalk(tree.Root);

        for (var run = 0; run < RUNS; run++)
        {
            var paths = FastWalkAll(lister, tree.Root).Paths;

            await Assert.That(expected.SetEquals(paths)).IsTrue().Because($"run {run} saw a different set");
            await Assert.That(paths.Count).IsEqualTo(expected.Count).Because($"run {run} visited an entry twice");
        }
    }

    // a visitor blowing up on some worker thread must reach the caller, not kill the process
    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task A_throwing_visitor_surfaces_on_the_caller(string lister)
    {
        using var tree = FixtureTree.Create(fillerFiles: 50);

        await Assert.That(() => Walker(lister).Walk(tree.Root, () => (ref _) => throw new InvalidOperationException("boom"), NoDeadline))
            .Throws<InvalidOperationException>();
    }

    // workers must be done when Walk returns, cut short or not
    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task Never_visits_after_walk_returns(string lister)
    {
        using var tree = FixtureTree.Create(fillerFiles: 500);
        var visits = 0;
        var deadline = Deadline.After(TimeSpan.FromMilliseconds(1), System.Diagnostics.Stopwatch.GetTimestamp());

        Walker(lister).Walk(tree.Root, () => (ref _) => Interlocked.Increment(ref visits), deadline);
        var atReturn = Volatile.Read(ref visits);
        await Task.Delay(200);

        await Assert.That(Volatile.Read(ref visits)).IsEqualTo(atReturn);
    }
}
