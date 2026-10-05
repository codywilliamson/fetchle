namespace Fetchle.Core.Walking;

// one thread's share of a walk: drain my own deque, steal when it's empty, quit when nothing is in flight
sealed class WalkWorker(WalkState walk, int index, IDirectoryLister lister)
{
    readonly WorkDeque _dirs = new();
    FastWalker.EntryVisitor _visit = null!;

    public PruneRules Prune => walk.Prune;

    public bool ShouldStop() => walk.ShouldStop();

    public void Visit(ref WalkEntry entry) => _visit(ref entry);

    // counted before it's pushed, so in-flight never reads 0 while a dir is waiting in some deque
    public void Push(DirTask dir)
    {
        walk.AddInFlight();
        _dirs.PushEnd(dir);
    }

    public bool TryStealFront(out DirTask dir) => _dirs.TryStealFront(out dir);

    public void Run()
    {
        _visit = walk.MakeVisitor();
        var idle = new SpinWait();

        while (!walk.ShouldStop())
        {
            if (_dirs.TryTakeEnd(out var dir) || walk.TrySteal(index, out dir))
            {
                if (!lister.List(dir, this))
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

    // after the walk: hand back every task nobody listed, then let the lister go
    public void Close()
    {
        while (_dirs.TryTakeEnd(out var dir))
        {
            lister.Discard(dir);
        }

        lister.Dispose();
    }
}
