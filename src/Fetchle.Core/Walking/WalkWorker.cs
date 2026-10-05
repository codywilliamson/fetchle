using System.Diagnostics.CodeAnalysis;
using System.IO.Enumeration;

namespace Fetchle.Core.Walking;

// one thread's share of a walk: drain my own deque, steal when it's empty, quit when nothing is in flight
sealed class WalkWorker(WalkState walk, int index)
{
    readonly WorkDeque _dirs = new();
    FastWalker.EntryVisitor _visit = null!;

    public PruneRules Prune => walk.Prune;

    public void Visit(ref FileSystemEntry entry) => _visit(ref entry);

    // counted before it's pushed, so in-flight never reads 0 while a dir is waiting in some deque
    public void Push(string dir)
    {
        walk.AddInFlight();
        _dirs.PushEnd(dir);
    }

    public bool TryStealFront([NotNullWhen(true)] out string? dir) => _dirs.TryStealFront(out dir);

    public void Run()
    {
        _visit = walk.MakeVisitor();
        var idle = new SpinWait();

        while (!walk.ShouldStop())
        {
            if (_dirs.TryTakeEnd(out var dir) || walk.TrySteal(index, out dir))
            {
                if (!List(dir))
                {
                    return;
                }

                // only finished once its subdirs are counted, so this can't drop in-flight to 0 early
                walk.FinishInFlight();
                idle.Reset();
                continue;
            }

            if (walk.NothingInFlight)
            {
                return;
            }

            // never Sleep(1): on windows that can park an idle worker for a whole 15 ms timer tick
            idle.SpinOnce(sleep1Threshold: -1);
        }
    }

    // false when the walk got stopped partway through this dir
    bool List(string dir)
    {
        using var lister = new DirectoryLister(dir, this);
        while (lister.MoveNext())
        {
            if (walk.ShouldStop())
            {
                return false;
            }
        }

        return true;
    }
}
