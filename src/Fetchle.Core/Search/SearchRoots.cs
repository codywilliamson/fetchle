using Fetchle.Core.Storage;

namespace Fetchle.Core.Search;

// the one place requested roots become walkable ones, so the walker and the index agree on what a request covers
static class SearchRoots
{
    // full paths, each must exist; a root under another requested root is dropped, it would only repeat entries
    public static List<string> Resolve(IReadOnlyList<string> requested)
    {
        var roots = new List<string>(requested.Count);
        foreach (var raw in requested)
        {
            var root = FileSearch.NormalizeRoot(raw);
            if (!Directory.Exists(root))
            {
                throw InvalidRootException.NotFound(raw);
            }

            if (roots.Exists(r => RootsConfig.Covers(r, root)))
            {
                continue;
            }

            roots.RemoveAll(r => RootsConfig.Covers(root, r));
            roots.Add(root);
        }
        return roots;
    }
}
