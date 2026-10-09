using Fetchle.Core.Search;
using Fetchle.Core.Walking;
using Fetchle.Fixtures;
using static Fetchle.Tests.Core.WalkHelpers;

namespace Fetchle.Tests.Core;

// what only matters where the native lister exists: odd names, link roots, leaked handles
public class WindowsWalkerTests
{
    // \u005c is a backslash. \?\ turns off win32 name cleanup, which is the only way to make these dirs
    const string EXTENDED_PREFIX = "\u005c\u005c?\u005c";
    static readonly string[] OddDirs = ["dot.", "space ", "CON"];

    static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fetchle-windows-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    static void DeleteExtended(string root) => Directory.Delete(EXTENDED_PREFIX + root, recursive: true);

    [Test]
    public async Task Lists_trailing_dot_trailing_space_and_reserved_name_dirs_literally()
    {
        Skip.Unless(NativeAvailable, "native lister is windows only");
        var root = TempDir();
        try
        {
            foreach (var odd in OddDirs)
            {
                try
                {
                    Directory.CreateDirectory(EXTENDED_PREFIX + Path.Combine(root, odd));
                    File.WriteAllText(EXTENDED_PREFIX + Path.Combine(root, odd, "inner.txt"), "x");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    Skip.Test("can't create odd names here");
                }
            }

            var seen = FastWalk(NATIVE, root);

            foreach (var odd in OddDirs)
            {
                await Assert.That(seen.Contains(Path.Combine(root, odd))).IsTrue();
                await Assert.That(seen.Contains(Path.Combine(root, odd, "inner.txt"))).IsTrue();
            }

            await Assert.That(seen.Count).IsEqualTo(OddDirs.Length * 2);
        }
        finally
        {
            DeleteExtended(root);
        }
    }

    // today's behavior: a junction as the root is followed once, links inside it never are
    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task A_junction_root_is_walked_like_the_naive_walker_does(string lister)
    {
        using var tree = FixtureTree.Create(fillerFiles: 10);
        Skip.Unless(tree.Created.HasFlag(FixtureFeatures.Junction), "can't create links here");
        var junction = tree.Full(FixtureTree.JUNCTION_DIR);

        var seen = FastWalk(lister, junction);

        await Assert.That(seen.Count).IsGreaterThan(0);
        await Assert.That(seen.OrderBy(p => p, StringComparer.Ordinal)).IsEquivalentTo(NaiveWalk(junction).OrderBy(p => p, StringComparer.Ordinal));
    }

    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task A_root_with_a_trailing_separator_reports_the_same_paths(string lister)
    {
        using var tree = FixtureTree.Create(fillerFiles: 10);

        var withSeparator = FastWalk(lister, tree.Root + Path.DirectorySeparatorChar);

        await Assert.That(withSeparator.OrderBy(p => p, StringComparer.Ordinal)).IsEquivalentTo(FastWalk(lister, tree.Root).OrderBy(p => p, StringComparer.Ordinal));
    }

    // windows paths are case-insensitive, so a root typed in other casing must still walk everything
    [Test]
    [MethodDataSource(typeof(WalkHelpers), nameof(Listers))]
    public async Task A_root_in_other_casing_reports_the_same_entries(string lister)
    {
        Skip.Unless(OperatingSystem.IsWindows(), "only windows paths are case-insensitive by default");
        using var tree = FixtureTree.Create(fillerFiles: 10);
        var otherCasing = tree.Root.ToUpperInvariant() == tree.Root ? tree.Root.ToLowerInvariant() : tree.Root.ToUpperInvariant();

        var seen = FastWalk(lister, otherCasing);

        await Assert.That(seen.Count).IsGreaterThan(0);
        await Assert.That(seen.SetEquals(FastWalk(lister, tree.Root))).IsTrue();
    }

    // the handle counter is process wide, so these can't overlap other walks
    [Test]
    [NotInParallel]
    public async Task A_normal_walk_closes_every_handle()
    {
        Skip.Unless(NativeAvailable, "native lister is windows only");
        using var tree = FixtureTree.Create(fillerFiles: 200);

        FastWalk(NATIVE, tree.Root);

        await Assert.That(LiveNativeHandles).IsEqualTo(0);
    }

    [Test]
    [NotInParallel]
    public async Task A_deadline_cut_walk_closes_every_handle()
    {
        Skip.Unless(NativeAvailable, "native lister is windows only");
        using var tree = FixtureTree.Create(fillerFiles: 500);
        var deadline = Deadline.After(TimeSpan.FromMilliseconds(1), System.Diagnostics.Stopwatch.GetTimestamp());

        Walker(NATIVE).Walk(tree.Root, () => (ref _) => { }, deadline);

        await Assert.That(LiveNativeHandles).IsEqualTo(0);
    }

    // stops the walk after a few entries, so plenty of pushed dirs are never opened
    [Test]
    [NotInParallel]
    public async Task A_throwing_visitor_closes_every_handle()
    {
        Skip.Unless(NativeAvailable, "native lister is windows only");
        using var tree = FixtureTree.Create(fillerFiles: 200);
        var visits = 0;

        await Assert.That(() => Walker(NATIVE).Walk(tree.Root, () => (ref _) =>
        {
            if (Interlocked.Increment(ref visits) > 30)
            {
                throw new InvalidOperationException("boom");
            }
        }, NoDeadline)).Throws<InvalidOperationException>();

        await Assert.That(LiveNativeHandles).IsEqualTo(0);
    }
}
