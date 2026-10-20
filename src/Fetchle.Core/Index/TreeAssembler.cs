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
        var groupsBySpan = groups.GetAlternateLookup<ReadOnlySpan<char>>();
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
        var segments = new SegmentTable();
        // one queue for every root: dequeuing in id order is what keeps the parent column from ever decreasing
        // a queued dir carries its group, found once when its parent placed it, so nothing re-hashes its path
        var queue = new Queue<(uint Id, GroupRef Group, int Root)>();
        var childPath = new char[1024];
        var perRoot = new int[walks.Count];
        var filesPerRoot = new int[walks.Count];
        var next = (uint)walks.Count;
        var attached = 0;

        for (var r = 0; r < walks.Count; r++)
        {
            parents[r] = SnapshotFormat.NO_PARENT;
            names[r] = (uint)r;
            flags[r] = SnapshotFormat.FLAG_DIRECTORY | SnapshotFormat.FLAG_ROOT;
            if (groups.TryGetValue(walks[r].Path, out var rootGroup))
            {
                queue.Enqueue(((uint)r, rootGroup, r));
            }
        }

        while (queue.TryDequeue(out var dir))
        {
            attached++;
            var collector = dir.Group.Collector;
            var group = collector.Groups[dir.Group.Group];
            for (var e = group.First; e < group.First + group.Count; e++)
            {
                var name = collector.Name(e);
                var id = next++;
                parents[id] = dir.Id;
                names[id] = (uint)segments.Intern(name);
                perRoot[dir.Root]++;
                var isDirectory = collector.IsDirectory(e);
                filesPerRoot[dir.Root] += isDirectory ? 0 : 1;
                flags[id] = isDirectory ? SnapshotFormat.FLAG_DIRECTORY : (byte)0;
                // the walker spells a child dir as Path.Join(parent, name), so the same spelling finds its group.
                // a dir with no group was empty or unreadable and has no children to place
                if (isDirectory && groupsBySpan.TryGetValue(Join(ref childPath, group.Directory, name), out var childGroup))
                {
                    queue.Enqueue((id, childGroup, dir.Root));
                }
            }
        }

        var roots = new List<TreeRoot>(walks.Count);
        for (var r = 0; r < walks.Count; r++)
        {
            roots.Add(new TreeRoot(walks[r].Path, perRoot[r], filesPerRoot[r], walks[r].WalkTime));
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

    // Path.Join(directory, name) into a reused buffer
    static ReadOnlySpan<char> Join(ref char[] buffer, string directory, ReadOnlySpan<char> name)
    {
        var separator = Path.EndsInDirectorySeparator(directory) ? 0 : 1;
        var length = directory.Length + separator + name.Length;
        if (buffer.Length < length)
        {
            buffer = new char[length * 2];
        }

        directory.CopyTo(buffer);
        if (separator == 1)
        {
            buffer[directory.Length] = Path.DirectorySeparatorChar;
        }
        name.CopyTo(buffer.AsSpan(directory.Length + separator));
        return buffer.AsSpan(0, length);
    }
}
