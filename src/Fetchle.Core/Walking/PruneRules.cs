namespace Fetchle.Core.Walking;

// directories dropped before descending, docs/architecture.md#building-the-index
public sealed class PruneRules
{
    static readonly string[] DefaultNames = ["node_modules", ".git"];

    readonly string[] _names;
    readonly string[] _paths;

    public PruneRules(string[] names, string[] paths)
    {
        _names = names;
        _paths = Array.ConvertAll(paths, p => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p)));
    }

    public static PruneRules Default { get; } = new(DefaultNames, DefaultPaths());

    public bool ShouldPrune(ReadOnlySpan<char> parentDirectory, ReadOnlySpan<char> name)
    {
        foreach (var n in _names)
            if (name.Equals(n, PathComparison.Default)) return true;
        foreach (var p in _paths)
        {
            // only compare the full path when the last segment matches, so nothing allocates
            var pathName = Path.GetFileName(p.AsSpan());
            if (name.Equals(pathName, PathComparison.Default)
                && Path.TrimEndingDirectorySeparator(parentDirectory).Equals(Path.GetDirectoryName(p.AsSpan()), PathComparison.Default))
                return true;
        }
        return false;
    }

    static string[] DefaultPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var paths = new List<string> { Path.GetTempPath() };
        if (OperatingSystem.IsWindows())
            paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"));
        else if (OperatingSystem.IsMacOS())
            paths.Add(Path.Combine(home, "Library", "Caches"));
        else
            paths.Add(Path.Combine(home, ".cache"));
        return [.. paths];
    }
}
