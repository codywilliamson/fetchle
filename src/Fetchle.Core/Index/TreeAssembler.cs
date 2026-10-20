namespace Fetchle.Core.Index;

// one root's walk: its normalized path, every worker's collector and how long the walk took
sealed record RootWalk(string Path, List<TreeCollector> Collectors, TimeSpan WalkTime);

// turns the per-worker groups of one or more walks into a PathTree: roots first, then breadth-first from each
// root, so every parent id is below its children's and children of one dir sit next to each other
static class TreeAssembler
{
    readonly record struct GroupRef(TreeCollector Collector, int Group);

    public static PathTree Assemble(IReadOnlyList<RootWalk> walks)
    {
        var groups = new Dictionary<string, GroupRef>(StringComparer.Ordinal);
        var total = walks.Count;
        foreach (var walk in walks)
        {
            foreach (var collector in walk.Collectors)
            {
                total += collector.Count;
                for (var g = 0; g < collector.Groups.Count; g++)
                {
                    groups.TryAdd(collector.Groups[g].Directory, new GroupRef(collector, g));
                }
            }
        }

        var parents = new uint[total];
        var names = new uint[total];
        var flags = new byte[total];
        var segments = new List<string>();
        var segmentIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var lookup = segmentIds.GetAlternateLookup<ReadOnlySpan<char>>();
        // one queue for every root: dequeuing in id order is what keeps the parent column from ever decreasing
        var queue = new Queue<(uint Id, string Path, int Root)>();
        var perRoot = new int[walks.Count];
        var next = (uint)walks.Count;
        var attached = 0;

        for (var r = 0; r < walks.Count; r++)
        {
            parents[r] = SnapshotFormat.NO_PARENT;
            names[r] = (uint)r;
            flags[r] = SnapshotFormat.FLAG_DIRECTORY | SnapshotFormat.FLAG_ROOT;
            queue.Enqueue(((uint)r, walks[r].Path, r));
        }

        while (queue.TryDequeue(out var dir))
        {
            if (!groups.TryGetValue(dir.Path, out var groupRef))
            {
                continue;
            }

            attached++;
            var collector = groupRef.Collector;
            var group = collector.Groups[groupRef.Group];
            for (var e = group.First; e < group.First + group.Count; e++)
            {
                var name = collector.Name(e);
                var id = next++;
                parents[id] = dir.Id;
                names[id] = (uint)Intern(name, segments, ref lookup);
                perRoot[dir.Root]++;
                var isDirectory = collector.IsDirectory(e);
                flags[id] = isDirectory ? SnapshotFormat.FLAG_DIRECTORY : (byte)0;
                if (isDirectory)
                {
                    // the walker spells a child dir as Path.Join(parent, name), so the same spelling finds its group
                    queue.Enqueue((id, Path.Join(dir.Path, name), dir.Root));
                }
            }
        }

        var roots = new List<TreeRoot>(walks.Count);
        for (var r = 0; r < walks.Count; r++)
        {
            roots.Add(new TreeRoot(walks[r].Path, perRoot[r], walks[r].WalkTime));
        }

        // entries in groups that never attached are dropped, so trim to what was placed
        if (next != total)
        {
            Array.Resize(ref parents, (int)next);
            Array.Resize(ref names, (int)next);
            Array.Resize(ref flags, (int)next);
        }

        return new PathTree(parents, names, flags, segments, roots, groups.Count - attached);
    }

    // a string is only made the first time a name is seen
    static int Intern(ReadOnlySpan<char> name, List<string> segments, ref Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> lookup)
    {
        if (lookup.TryGetValue(name, out var id))
        {
            return id;
        }

        id = segments.Count;
        var segment = name.ToString();
        segments.Add(segment);
        lookup.Dictionary.Add(segment, id);
        return id;
    }
}
