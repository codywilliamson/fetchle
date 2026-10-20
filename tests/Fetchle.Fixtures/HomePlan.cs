namespace Fetchle.Fixtures;

// the directory tree of the home corpus before any of it touches disk. file names are not stored,
// only a count per dir, and the writer regenerates the names from the seed
sealed class HomePlan(int target)
{
    // keeps paths comfortably under MAX_PATH once the corpus root is prepended
    public const int MAX_RELATIVE_PATH_CHARS = 150;

    static readonly string[] PrunedNames = [".git", "node_modules", ".cache"];

    readonly Dictionary<string, int> _index = new(StringComparer.OrdinalIgnoreCase) { [""] = 0 };

    public int Target { get; } = target;

    // index 0 is the corpus root
    public List<string> Paths { get; } = [""];

    public List<int> Depths { get; } = [0];

    public List<FileKind> Kinds { get; } = [FileKind.Data];

    public List<int> GeneratedFiles { get; } = [0];

    public Dictionary<int, List<string>> FixedFiles { get; } = [];

    public long FileTotal { get; private set; }

    public int Entries => Paths.Count - 1 + (int)FileTotal;

    public int Remaining => Target - Entries;

    // adds the dir and any missing ancestors, returns the dir's index
    public int AddDir(string path, FileKind kind)
    {
        if (_index.TryGetValue(path, out var existing))
        {
            return existing;
        }

        var slash = path.LastIndexOf('/');
        var parent = AddDir(slash < 0 ? "" : path[..slash], kind);
        var index = Paths.Count;
        Paths.Add(path);
        Depths.Add(Depths[parent] + 1);
        Kinds.Add(kind);
        GeneratedFiles.Add(0);
        _index[path] = index;
        return index;
    }

    // -1 when the dir is already there, so a caller growing a tree can retry with another name
    public int AddNewDir(string path, FileKind kind) => _index.ContainsKey(path) ? -1 : AddDir(path, kind);

    public void AddGeneratedFiles(int dir, int count)
    {
        GeneratedFiles[dir] += count;
        FileTotal += count;
    }

    public void AddFixedFile(int dir, string name)
    {
        if (!FixedFiles.TryGetValue(dir, out var names))
        {
            FixedFiles[dir] = names = [];
        }
        names.Add(name);
        FileTotal++;
    }

    public HomeCorpusStats Stats()
    {
        var prunedDirs = 0;
        var prunedBelow = 0;
        for (var i = 1; i < Paths.Count; i++)
        {
            var files = GeneratedFiles[i] + (FixedFiles.TryGetValue(i, out var fixedNames) ? fixedNames.Count : 0);
            switch (Locate(Paths[i]))
            {
                case PruneSpot.Root:
                    prunedDirs++;
                    prunedBelow += files;
                    break;
                case PruneSpot.Inside:
                    prunedBelow += 1 + files;
                    break;
            }
        }
        return new HomeCorpusStats(Paths.Count - 1, (int)FileTotal, prunedDirs, prunedBelow, Depths.Max());
    }

    enum PruneSpot { Outside, Root, Inside }

    static PruneSpot Locate(string path)
    {
        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            if (Array.IndexOf(PrunedNames, segments[i]) >= 0)
            {
                return i == segments.Length - 1 ? PruneSpot.Root : PruneSpot.Inside;
            }
        }
        return PruneSpot.Outside;
    }
}
