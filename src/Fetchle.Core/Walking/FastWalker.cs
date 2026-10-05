using System.IO.Enumeration;
using Fetchle.Core.Search;

namespace Fetchle.Core.Walking;

// - visits every file and directory under root, same set as NaiveWalker
// - never visits or descends into dirs PruneRules prunes
// - skips reparse points (junctions, symlinks) so loops end
// - skips unreadable dirs without throwing
// - empty, missing or file root visits nothing and doesn't throw
// - returns false only when the deadline cut the walk short
public sealed class FastWalker(PruneRules prune)
{
    readonly PruneRules _prune = prune;

    static readonly EnumerationOptions Options = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public delegate void EntryVisitor(ref FileSystemEntry entry);

    // called once per worker, from that worker's thread, so each worker gets a private visitor
    // and nothing is shared mid-walk. the caller merges whatever its visitors collected
    public delegate EntryVisitor VisitorFactory();

    public bool Walk(string root, VisitorFactory makeVisitor, Deadline deadline)
    {
        ArgumentNullException.ThrowIfNull(root);

        // a missing or file root has nothing to walk, that's a finished walk not a budget cut
        if (!Directory.Exists(root))
        {
            return true;
        }

        if (deadline.IsExpired())
        {
            return false;
        }

        var visit = makeVisitor();
        var dirs = new Stack<string>();
        dirs.Push(root);

        while (dirs.TryPop(out var dir))
        {
            using var lister = new DirectoryLister(dir, _prune, visit, dirs);
            while (lister.MoveNext())
            {
                if (deadline.IsExpired())
                {
                    return false;
                }
            }
        }

        return true;
    }

    // lists one directory: visits each entry, queues unpruned subdirectories.
    // overrides instead of delegates, so no delegate or enumerable wrapper per dir
    sealed class DirectoryLister(
        string dir,
        PruneRules prune,
        EntryVisitor visit,
        Stack<string> dirs) : FileSystemEnumerator<byte>(dir, Options)
    {
        protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
        {
            if (entry.IsDirectory)
            {
                if (prune.ShouldPrune(entry.Directory, entry.FileName))
                {
                    return false;
                }

                dirs.Push(entry.ToFullPath());
            }

            visit(ref entry);
            return true;
        }

        protected override byte TransformEntry(ref FileSystemEntry entry) => 0;
    }
}
