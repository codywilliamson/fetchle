using System.IO.Enumeration;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;

namespace Fetchle.Core.Naive;

// PLACEHOLDER: single-threaded recursive walk. the parallel work-stealing walker replaces this.
// exists so e2e and bench have something real to drive and the real walker has a baseline
public sealed class NaiveWalker(PruneRules prune)
{
    public delegate void EntryVisitor(ref FileSystemEntry entry);

    static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        // include hidden and system, skip reparse points so junctions and symlink loops aren't followed
        AttributesToSkip = FileAttributes.ReparsePoint,
        ReturnSpecialDirectories = false,
    };

    // returns false if the deadline cut the walk short
    public bool Walk(string root, EntryVisitor visit, Deadline deadline)
    {
        // an empty root never reaches the predicates, so check once up front
        if (deadline.IsExpired()) return false;
        var stopped = false;
        var enumerable = new FileSystemEnumerable<byte>(root, static (ref _) => 0, Options)
        {
            ShouldRecursePredicate = (ref entry) =>
                !(stopped |= deadline.IsExpired()) && !prune.ShouldPrune(entry.Directory, entry.FileName),
            ShouldIncludePredicate = (ref entry) =>
            {
                if (stopped |= deadline.IsExpired()) return false;
                if (entry.IsDirectory && prune.ShouldPrune(entry.Directory, entry.FileName)) return false;
                visit(ref entry);
                return false;
            },
        };
        foreach (var _ in enumerable) { }
        return !stopped;
    }
}
