using System.Runtime.ExceptionServices;
using Fetchle.Core.Search;

namespace Fetchle.Core.Walking;

// what every worker of one walk shares: each other (for stealing), the in-flight count and the stop flag
sealed class WalkState
{
    readonly Deadline _deadline;
    readonly WalkWorker[] _workers;
    int _inFlight;
    volatile bool _stopped;
    ExceptionDispatchInfo? _error;

    public WalkState(PruneRules prune, FastWalker.VisitorFactory makeVisitor, Deadline deadline, int workerCount, ListerKind lister)
    {
        Prune = prune;
        MakeVisitor = makeVisitor;
        _deadline = deadline;
        _workers = new WalkWorker[workerCount];
        for (var i = 0; i < workerCount; i++)
        {
            _workers[i] = new WalkWorker(this, i, DirectoryListers.Create(lister));
        }
    }

    public PruneRules Prune { get; }

    public FastWalker.VisitorFactory MakeVisitor { get; }

    public bool NothingInFlight => Volatile.Read(ref _inFlight) == 0;

    public void AddInFlight() => Interlocked.Increment(ref _inFlight);

    public void FinishInFlight() => Interlocked.Decrement(ref _inFlight);

    public bool ShouldStop()
    {
        if (!_stopped && _deadline.IsExpired())
        {
            _stopped = true;
        }

        return _stopped;
    }

    // victims in order from my right-hand neighbour, so thieves spread out instead of all hitting worker 0
    public bool TrySteal(int thief, out DirTask dir)
    {
        for (var offset = 1; offset < _workers.Length; offset++)
        {
            if (_workers[(thief + offset) % _workers.Length].TryStealFront(out dir))
            {
                return true;
            }
        }

        dir = default;
        return false;
    }

    // returns false if the deadline cut the walk short
    public bool Run(string root)
    {
        try
        {
            _workers[0].Push(new DirTask(root, null));

            // worker 0 runs on the calling thread, the rest get their own
            var threads = new Thread[_workers.Length - 1];
            for (var i = 0; i < threads.Length; i++)
            {
                var worker = _workers[i + 1];
                threads[i] = new Thread(() => RunGuarded(worker)) { IsBackground = true, Name = $"fetchle-walk-{i + 1}" };
                threads[i].Start();
            }

            RunGuarded(_workers[0]);
            foreach (var thread in threads)
            {
                thread.Join();
            }
        }
        finally
        {
            // every worker is done, so whatever is left in the deques was never listed
            foreach (var worker in _workers)
            {
                worker.Close();
            }
        }

        _error?.Throw();
        return !_stopped;
    }

    // a throwing visitor stops everyone, then rethrows on the caller's thread after all workers are done
    void RunGuarded(WalkWorker worker)
    {
        try
        {
            worker.Run();
        }
        catch (Exception ex)
        {
            Interlocked.CompareExchange(ref _error, ExceptionDispatchInfo.Capture(ex), null);
            _stopped = true;
        }
    }
}
