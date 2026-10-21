namespace Fetchle.Core.Index;

// a PathTree from relative paths under one root, with nothing on disk: evals and benches build corpora of a
// million entries this way in seconds. missing parent dirs are added. paths use '/' and never start with it
static class TreeFromPaths
{
    public static PathTree Build(string root, IEnumerable<(string Relative, bool IsDirectory)> entries)
    {
        root = Path.TrimEndingDirectorySeparator(root);
        var children = new Dictionary<string, List<(string Name, bool IsDirectory)>>(StringComparer.Ordinal) { [""] = [] };
        foreach (var (relative, isDirectory) in entries)
        {
            Add(children, relative.TrimEnd('/'), isDirectory);
        }

        var collector = new TreeCollector();
        foreach (var (dir, names) in children)
        {
            var full = dir.Length == 0 ? root : Path.Join(root, dir.Replace('/', Path.DirectorySeparatorChar));
            foreach (var (name, isDirectory) in names)
            {
                collector.Add(full, name, isDirectory);
            }
        }
        return TreeAssembler.Assemble([new RootWalk(root, [collector], TimeSpan.Zero)]);
    }

    static void Add(Dictionary<string, List<(string, bool)>> children, string relative, bool isDirectory)
    {
        if (relative.Length == 0)
        {
            return;
        }

        var slash = relative.LastIndexOf('/');
        var parent = slash < 0 ? "" : relative[..slash];
        if (isDirectory)
        {
            if (children.ContainsKey(relative))
            {
                return;
            }
            children[relative] = [];
        }

        if (!children.TryGetValue(parent, out var siblings))
        {
            Add(children, parent, isDirectory: true);
            siblings = children[parent];
        }
        siblings.Add((relative[(slash + 1)..], isDirectory));
    }
}
