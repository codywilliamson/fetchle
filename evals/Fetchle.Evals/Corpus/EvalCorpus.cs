using Fetchle.Core.Index;
using Fetchle.Core.Walking;
using Fetchle.Fixtures;

namespace Fetchle.Evals.Corpus;

// the eval's home dir, built straight into a snapshot with nothing on disk: the seeded 1M-entry home corpus
// for scale and realistic filler, plus evals/corpus/paths.txt, the real paths behind the mined queries and the
// listings around them. default prune rules apply the way the walker would apply them
public sealed class EvalCorpus : IDisposable
{
    public const int HOME_ENTRIES = 1_000_000;
    // the walker prunes the os temp dir by path; the corpus spells it the windows way
    const string TEMP_DIR = "AppData/Local/Temp";

    EvalCorpus(string root, Snapshot snapshot, HashSet<string> paths)
    {
        Root = root;
        Snapshot = snapshot;
        Paths = paths;
    }

    // a path that's never created; hits come back under it
    public string Root { get; }

    internal Snapshot Snapshot { get; }

    // every relative path in the corpus before pruning, dirs without their trailing slash
    public HashSet<string> Paths { get; }

    public static EvalCorpus Build(string pathsFile)
    {
        var all = new List<(string Path, bool IsDirectory)>(HOME_ENTRIES + 20_000);
        all.AddRange(HomeCorpus.Entries(HOME_ENTRIES));
        foreach (var line in File.ReadLines(pathsFile))
        {
            if (line.Length > 0)
            {
                all.Add((line.TrimEnd('/'), line.EndsWith('/')));
            }
        }

        var paths = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<(string, bool)>(all.Count);
        foreach (var (path, isDirectory) in all)
        {
            paths.Add(path);
            if (!IsPruned(path))
            {
                kept.Add((path, isDirectory));
            }
        }

        var root = Path.Join(Path.GetTempPath(), "fetchle-eval-home-never-created");
        var tree = TreeFromPaths.Build(root, kept);
        var snapshot = Snapshot.FromBytes(SnapshotWriter.ToBytes(tree, DateTimeOffset.UnixEpoch), "eval corpus");
        return new EvalCorpus(Path.TrimEndingDirectorySeparator(root), snapshot, paths);
    }

    // the walker's default name rules on any segment, plus the temp dir
    public static bool IsPruned(string relative)
    {
        relative = relative.TrimEnd('/');
        if (relative.StartsWith(TEMP_DIR + "/", StringComparison.OrdinalIgnoreCase) || relative.Equals(TEMP_DIR, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var segment in relative.Split('/'))
        {
            if (PruneRules.Default.ShouldPrune("", segment))
            {
                return true;
            }
        }
        return false;
    }

    // one line of sizes, for docs and diagrams
    public string Describe()
    {
        var lexicon = Snapshot.Lexicon;
        return $"corpus: {Snapshot.EntryCount:N0} entries, {Snapshot.SegmentCount:N0} unique segments, {lexicon.TokenCount:N0} tokens, {lexicon.PostingCount:N0} postings, {lexicon.FuzzyKeyCount:N0} fuzzy keys, "
            + $"snapshot {Snapshot.Image.Length / 1024.0 / 1024:F1} MB ({(double)Snapshot.Image.Length / Snapshot.EntryCount:F1} B/entry)";
    }

    public void Dispose() => Snapshot.Dispose();
}
