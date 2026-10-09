namespace Fetchle.Core.Walking;

// lists one dir at a time for one worker. one instance per worker, so it may keep per-worker buffers
interface IDirectoryLister : IDisposable
{
    // visits every entry and pushes unpruned subdirs to owner. false when the walk was stopped partway.
    // the lister owns the task's Parent for the whole call, even if it throws
    bool List(in DirTask dir, WalkWorker owner);

    // a task that was pushed but never listed because the walk ended. releases its Parent, never throws
    void Discard(in DirTask dir);
}
