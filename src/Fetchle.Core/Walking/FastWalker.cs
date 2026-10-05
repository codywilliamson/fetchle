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
        RecurseSubdirectories = true,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public delegate void EntryVisitor(ref FileSystemEntry entry);

    public bool Walk(string root, EntryVisitor visit, Deadline deadline)
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

        var files = new FileSystemEnumerable<byte>(root, static (ref _) => 0, Options)
        {
            ShouldIncludePredicate = (ref e) =>
            {
                if (e.IsDirectory && _prune.ShouldPrune(e.Directory, e.FileName))
                {
                    return false;
                }
                visit(ref e);
                return true;
            },
            ShouldRecursePredicate = (ref e) => !_prune.ShouldPrune(e.Directory, e.FileName)
        };

        foreach (var _ in files)
        {
            if (deadline.IsExpired())
            {
                return false;
            }
        }

        return true;
    }
}
