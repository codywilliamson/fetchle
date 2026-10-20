namespace Fetchle.Core.Index;

public sealed record TreeRoot(string Path, int Entries, int Files, TimeSpan WalkTime);

// the in-memory store a snapshot is written from: struct-of-arrays in snapshot order (roots, then breadth-first)
sealed class PathTree(uint[] parents, uint[] names, byte[] flags, List<string> segments, List<TreeRoot> roots, int unattachedDirs)
{
    public uint[] Parents { get; } = parents;

    public uint[] Names { get; } = names;

    public byte[] Flags { get; } = flags;

    public List<string> Segments { get; } = segments;

    public List<TreeRoot> Roots { get; } = roots;

    // listed dirs whose parent entry never turned up. always 0 unless a lister spells a path two ways
    public int UnattachedDirs { get; } = unattachedDirs;

    public int EntryCount => Parents.Length;
}
