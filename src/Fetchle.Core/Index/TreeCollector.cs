using Fetchle.Core.Walking;

namespace Fetchle.Core.Index;

// one walker worker's share of a rebuild. a worker lists a dir in one go, so its entries arrive as runs that
// share a parent; each run becomes a group keyed by the parent's path. names go into one char arena, so
// nothing allocates per entry, only one string per listed dir
sealed class TreeCollector
{
    const int INITIAL_ENTRIES = 4096;
    const int INITIAL_CHARS = 64 * 1024;

    char[] _chars = new char[INITIAL_CHARS];
    int _charCount;
    int[] _nameStarts = new int[INITIAL_ENTRIES];
    int[] _nameLengths = new int[INITIAL_ENTRIES];
    bool[] _isDirectory = new bool[INITIAL_ENTRIES];

    public readonly record struct Group(string Directory, int First, int Count);

    public List<Group> Groups { get; } = [];

    public int Count { get; private set; }

    public void Visit(ref WalkEntry entry) => Add(entry.Directory, entry.FileName, entry.IsDirectory);

    public void Add(ReadOnlySpan<char> directory, ReadOnlySpan<char> name, bool isDirectory)
    {
        if (Groups.Count == 0 || !directory.SequenceEqual(Groups[^1].Directory))
        {
            Groups.Add(new Group(directory.ToString(), Count, 0));
        }

        if (Count == _nameStarts.Length)
        {
            Array.Resize(ref _nameStarts, Count * 2);
            Array.Resize(ref _nameLengths, Count * 2);
            Array.Resize(ref _isDirectory, Count * 2);
        }

        if (_charCount + name.Length > _chars.Length)
        {
            Array.Resize(ref _chars, Math.Max(_chars.Length * 2, _charCount + name.Length));
        }

        name.CopyTo(_chars.AsSpan(_charCount));
        _nameStarts[Count] = _charCount;
        _nameLengths[Count] = name.Length;
        _isDirectory[Count] = isDirectory;
        _charCount += name.Length;
        Count++;

        var last = Groups[^1];
        Groups[^1] = last with { Count = last.Count + 1 };
    }

    public ReadOnlySpan<char> Name(int entry) => _chars.AsSpan(_nameStarts[entry], _nameLengths[entry]);

    public bool IsDirectory(int entry) => _isDirectory[entry];
}
