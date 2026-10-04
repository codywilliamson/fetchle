using System.Collections.Frozen;

namespace Fetchle.Core.Walking;

public sealed class PruneRules
{
    static readonly string[] DefaultNames =
    [
        // version control
        ".git", ".svn", ".hg", ".bzr",
        // package and build caches
        "node_modules", "bower_components", ".next", ".nuxt", ".parcel-cache", ".turbo",
        "__pycache__", ".pytest_cache", ".mypy_cache", ".ruff_cache", ".tox", ".venv",
        ".gradle", ".terraform", ".vs", ".cache",
        // os trash and volume metadata
        "$RECYCLE.BIN", "System Volume Information", ".Trash", ".Trashes",
    ];

    readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _names;
    readonly string[] _paths;

    public PruneRules(string[] names, string[] paths)
    {
        _names = names.ToFrozenSet(PathComparison.Comparer).GetAlternateLookup<ReadOnlySpan<char>>();
        _paths = Array.ConvertAll(paths, p => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p)));
    }

    public static PruneRules Default { get; } = new(DefaultNames, DefaultPaths());

    public bool ShouldPrune(ReadOnlySpan<char> parentDirectory, ReadOnlySpan<char> name)
    {
        if (_names.Contains(name))
        {
            return true;
        }

        foreach (var p in _paths)
        {
            // only compare the full path when the last segment matches, so nothing allocates
            var pathName = Path.GetFileName(p.AsSpan());
            if (name.Equals(pathName, PathComparison.Default)
                && Path.TrimEndingDirectorySeparator(parentDirectory).Equals(Path.GetDirectoryName(p.AsSpan()), PathComparison.Default))
            {
                return true;
            }
        }
        return false;
    }

    static string[] DefaultPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var paths = new List<string> { Path.GetTempPath() };
        if (OperatingSystem.IsWindows())
        {
            paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"));
        }
        else if (OperatingSystem.IsMacOS())
        {
            paths.Add(Path.Combine(home, "Library", "Caches"));
        }
        else
        {
            paths.Add(Path.Combine(home, ".cache"));
        }

        return [.. paths];
    }
}
