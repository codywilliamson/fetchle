namespace Fetchle.Fixtures;

// a ~1M-entry fake home dir for the external bench: repos with node_modules and .git, app data, caches,
// documents. heavy-tailed files per dir, mostly shallow with a deep tail, a third of it under pruned dirs.
// Corpus.Landmarks are in it, so "settings" hits a handful of real targets
public static class HomeCorpus
{
    public const int DEFAULT_ENTRIES = 1_000_000;
    public const string CORPUS_DIR_VARIABLE = "FETCHLE_BENCH_CORPUS_DIR";

    // bump when the generated layout changes, so cached corpora on disk get rebuilt
    const int LAYOUT_VERSION = 1;

    // the plan only, no disk: what Ensure would build
    public static HomeCorpusStats Describe(int entries = DEFAULT_ENTRIES, int seed = Corpus.DEFAULT_SEED) =>
        HomePlanner.Build(entries, seed).Stats();

    // every entry Ensure would write, as (relative path with '/', is dir), without touching disk
    public static IEnumerable<(string Path, bool IsDirectory)> Entries(int entries = DEFAULT_ENTRIES, int seed = Corpus.DEFAULT_SEED)
    {
        var plan = HomePlanner.Build(entries, seed);
        for (var i = 0; i < plan.Paths.Count; i++)
        {
            var dir = plan.Paths[i];
            if (i > 0)
            {
                yield return (dir, true);
            }

            foreach (var name in HomeCorpusWriter.FileNames(plan, seed, i))
            {
                yield return (dir.Length == 0 ? name : $"{dir}/{name}", false);
            }
        }
    }

    public static string DefaultRoot(int entries = DEFAULT_ENTRIES, int seed = Corpus.DEFAULT_SEED)
    {
        var parent = Environment.GetEnvironmentVariable(CORPUS_DIR_VARIABLE);
        if (string.IsNullOrEmpty(parent))
        {
            parent = Path.GetTempPath();
        }
        return Path.Combine(parent, $"fetchle-home-corpus-{seed}-{entries}");
    }

    // the marker sits next to the root, not in it, so the tree holds exactly the counted entries.
    // it's written last, so an interrupted build gets redone
    public static HomeCorpusStats Ensure(string root, int entries = DEFAULT_ENTRIES, int seed = Corpus.DEFAULT_SEED)
    {
        var marker = $"{Path.TrimEndingDirectorySeparator(root)}.corpus-v{LAYOUT_VERSION}-{seed}-{entries}";
        if (File.Exists(marker) && Directory.Exists(root) && HomeCorpusStats.FromMarker(File.ReadAllText(marker)) is { } cached)
        {
            return cached;
        }

        if (File.Exists(marker))
        {
            File.Delete(marker);
        }
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        var plan = HomePlanner.Build(entries, seed);
        HomeCorpusWriter.Write(plan, root, seed);

        var stats = plan.Stats();
        File.WriteAllText(marker, stats.ToMarker());
        return stats;
    }
}
