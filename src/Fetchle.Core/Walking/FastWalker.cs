using System.IO.Enumeration;
using Fetchle.Core.Search;

namespace Fetchle.Core.Walking;

// stub, tests/Fetchle.Tests/Core/FastWalkerTests.cs is the target. expected behavior:
// - visits every file and directory under root, same set as NaiveWalker
// - never visits or descends into dirs PruneRules prunes
// - skips reparse points (junctions, symlinks) so loops end
// - skips unreadable dirs without throwing
// - empty, missing or file root visits nothing and doesn't throw
// - returns false only when the deadline cut the walk short
public sealed class FastWalker(PruneRules prune)
{
    readonly PruneRules _prune = prune;

    public delegate void EntryVisitor(ref FileSystemEntry entry);

    public bool Walk(string root, EntryVisitor visit, Deadline deadline) => throw new NotImplementedException();
}
