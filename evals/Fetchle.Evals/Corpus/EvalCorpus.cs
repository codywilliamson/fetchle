using System.Diagnostics;
using Fetchle.Core.Embedding;
using Fetchle.Core.Index;
using Fetchle.Core.Ranking.Semantic;
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

    EvalCorpus(string root, Snapshot snapshot, HashSet<string> paths, StaticModel model, SegmentVectors vectors, TimeSpan embedTime)
    {
        Root = root;
        Snapshot = snapshot;
        Paths = paths;
        Model = model;
        Vectors = vectors;
        EmbedTime = embedTime;
    }

    internal StaticModel Model { get; }

    internal SegmentVectors Vectors { get; }

    // how long embedding every segment and every path's centroid length took, for the record
    public TimeSpan EmbedTime { get; }

    // a path that's never created; hits come back under it
    public string Root { get; }

    internal Snapshot Snapshot { get; }

    // every relative path in the corpus before pruning, dirs without their trailing slash
    public HashSet<string> Paths { get; }

    // the model is required: the gate measures the ranking that ships, and that includes semantic
    public static EvalCorpus Build(string pathsFile, string modelPath)
    {
        var model = StaticModel.Open(modelPath)
            ?? throw new FileNotFoundException($"no model at {modelPath}; run `dotnet build.cs model` or set {StaticModel.ENV_PATH}");
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
        var embedding = Stopwatch.GetTimestamp();
        var parts = VectorBuilder.Build(snapshot, model);
        var vectors = SegmentVectors.FromBytes(VectorWriter.ToBytes(parts, snapshot.Checksum, model.Checksum), snapshot, model.Checksum);
        return new EvalCorpus(Path.TrimEndingDirectorySeparator(root), snapshot, paths, model, vectors, Stopwatch.GetElapsedTime(embedding));
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
            + $"snapshot {Snapshot.Image.Length / 1024.0 / 1024:F1} MB ({(double)Snapshot.Image.Length / Snapshot.EntryCount:F1} B/entry), "
            + $"vectors {Vectors.Codes.Length / 1024.0 / 1024:F1} MB codes + {Vectors.Norms.Length * sizeof(float) / 1024.0 / 1024:F1} MB norms, embedded in {EmbedTime.TotalMilliseconds:N0} ms";
    }

    public void Dispose()
    {
        Vectors.Dispose();
        Model.Dispose();
        Snapshot.Dispose();
    }
}
