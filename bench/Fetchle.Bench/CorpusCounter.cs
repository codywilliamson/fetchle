using Fetchle.Core.Search;
using Fetchle.Core.Walking;

namespace Fetchle.Bench;

// how many entries a corpus has on disk and how many the real walker visits with the default prune rules
public static class CorpusCounter
{
    public static CorpusCounts Count(string shape, string root)
    {
        var all = Walk(root, new PruneRules([], []));
        var visited = Walk(root, PruneRules.Default);
        return new CorpusCounts(shape, all.Dirs, all.Files, all.Dirs + all.Files, visited.Dirs, visited.Files, visited.Dirs + visited.Files);
    }

    static (int Dirs, int Files) Walk(string root, PruneRules prune)
    {
        // one counter per worker, summed after the walk, so nothing is shared mid-walk
        var counters = new List<int[]>();
        var completed = new FastWalker(prune).Walk(root, () =>
        {
            var counter = new int[2];
            lock (counters)
            {
                counters.Add(counter);
            }
            return (ref entry) => counter[entry.IsDirectory ? 0 : 1]++;
        }, Deadline.After(TimeSpan.MaxValue, 0));

        if (!completed)
        {
            throw new InvalidOperationException($"walk of {root} was cut short");
        }
        return (counters.Sum(c => c[0]), counters.Sum(c => c[1]));
    }
}
