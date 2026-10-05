using Fetchle.Core.Search;

namespace Fetchle.Core.Walking;

// parallel work-stealing walker, see docs/diagrams/parallel-walker.excalidraw
// - visits every file and directory under root, same set as NaiveWalker
// - never visits or descends into dirs PruneRules prunes
// - skips reparse points (junctions, symlinks) so loops end
// - skips unreadable dirs without throwing
// - empty, missing or file root visits nothing and doesn't throw
// - returns false only when the deadline cut the walk short
// - no visit happens after Walk returns
public sealed class FastWalker(PruneRules prune)
{
    readonly PruneRules _prune = prune;

    // the bench sweeps this; everything else gets one worker per core
    internal int WorkerCount { get; init; } = Environment.ProcessorCount;

    // tests and the bench force a lister here
    internal ListerKind Lister { get; init; } = ListerKind.Auto;

    public delegate void EntryVisitor(ref WalkEntry entry);

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

        return new WalkState(_prune, makeVisitor, deadline, WorkerCount, Lister).Run(root);
    }
}
