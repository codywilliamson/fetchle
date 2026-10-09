using System.Collections.Concurrent;
using Fetchle.Core.Naive;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;
using Fetchle.Core.Walking.Windows;

namespace Fetchle.Tests.Core;

// shared by the walker test classes. listers go in as names because ListerKind is internal
static class WalkHelpers
{
    public const string DOT_NET = nameof(ListerKind.DotNet);
    public const string NATIVE = nameof(ListerKind.Native);

    public static readonly Deadline NoDeadline = Deadline.After(TimeSpan.MaxValue, 0);

    // every lister this machine can run, so each walker test runs against all of them
    public static IEnumerable<string> Listers()
    {
        yield return DOT_NET;
        if (NativeAvailable)
        {
            yield return NATIVE;
        }
    }

    public static bool NativeAvailable => OperatingSystem.IsWindows() && NativeSupport.IsAvailable;

    public static int LiveNativeHandles => OperatingSystem.IsWindows() ? NtApi.LiveHandles : 0;

    public static FastWalker Walker(string lister, PruneRules? prune = null) =>
        new(prune ?? PruneRules.Default) { Lister = Enum.Parse<ListerKind>(lister) };

    // every visit, duplicates kept. each worker fills its own bucket, merged after the walk
    public static (bool Completed, List<string> Paths) FastWalkAll(string lister, string root, PruneRules? prune = null)
    {
        var buckets = new ConcurrentBag<List<string>>();
        var completed = Walker(lister, prune).Walk(root, () =>
        {
            var bucket = new List<string>();
            buckets.Add(bucket);
            return (ref entry) => bucket.Add(entry.ToFullPath());
        }, NoDeadline);
        return (completed, buckets.SelectMany(b => b).ToList());
    }

    public static HashSet<string> FastWalk(string lister, string root, PruneRules? prune = null) =>
        new(FastWalkAll(lister, root, prune).Paths, PathComparison.Comparer);

    public static HashSet<string> NaiveWalk(string root)
    {
        var seen = new HashSet<string>(PathComparison.Comparer);
        new NaiveWalker(PruneRules.Default).Walk(root, (ref entry) => seen.Add(entry.ToFullPath()), NoDeadline);
        return seen;
    }
}
