using System.Diagnostics;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;

namespace Fetchle.Core.Index;

// the index directory: one snapshot, its temp sibling while a rebuild writes, and the writer lock.
// swap protocol: lock, walk, write temp, flush to disk, rename over the snapshot, unlock.
// readers never look at the temp file, so a writer dying at any step leaves the old snapshot intact
public sealed class IndexStore(string directory)
{
    public const string SNAPSHOT_FILE = "snapshot.fidx";
    public const string TEMP_SUFFIX = ".tmp";
    public const string LOCK_FILE = "write.lock";

    public string Directory { get; } = directory;

    public string SnapshotPath => Path.Combine(Directory, SNAPSHOT_FILE);

    public string TempPath => SnapshotPath + TEMP_SUFFIX;

    public string LockPath => Path.Combine(Directory, LOCK_FILE);

    // crash tests stop a rebuild between steps here
    internal Action<RebuildStep>? BeforeStep { get; init; }

    // null when there's no snapshot. throws IndexUnreadableException when there is one but it can't be trusted
    public SnapshotInfo? ReadInfo()
    {
        using var snapshot = Snapshot.Open(SnapshotPath);
        return snapshot?.Info();
    }

    internal Snapshot? Open() => Snapshot.Open(SnapshotPath);

    // walks every root and swaps in a new snapshot. throws IndexBusyException when another writer holds the lock
    public RebuildReport Rebuild(IReadOnlyList<string> roots, PruneRules prune, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        System.IO.Directory.CreateDirectory(Directory);
        using var writeLock = AcquireLock();

        Step(RebuildStep.Walk);
        var walks = new List<RootWalk>(roots.Count);
        var walkTime = TimeSpan.Zero;
        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var walk = WalkRoot(root, prune);
            walkTime += walk.WalkTime;
            walks.Add(walk);
        }

        var built = Stopwatch.GetTimestamp();
        var tree = TreeAssembler.Assemble(walks);
        var image = SnapshotWriter.ToBytes(tree, DateTimeOffset.UtcNow);
        // checked before anything touches disk, so a writer bug can never replace a good snapshot with a bad one
        using var written = Snapshot.FromBytes(image, SnapshotPath);
        cancellationToken.ThrowIfCancellationRequested();

        var buildTime = Stopwatch.GetElapsedTime(built);
        var writing = Stopwatch.GetTimestamp();
        WriteAndSwap(image);
        Step(RebuildStep.Done);
        return new RebuildReport(written.Info(), walkTime, buildTime, Stopwatch.GetElapsedTime(writing), Stopwatch.GetElapsedTime(start));
    }

    // temp file, flushed, then renamed over the snapshot. returns bytes written
    internal long WriteAndSwap(byte[] image)
    {
        Step(RebuildStep.WriteTemp);
        using (var temp = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1))
        {
            temp.Write(image);
            Step(RebuildStep.Flush);
            temp.Flush(flushToDisk: true);
        }

        Step(RebuildStep.Rename);
        AtomicReplace.Move(TempPath, SnapshotPath);
        return image.Length;
    }

    internal static RootWalk WalkRoot(string root, PruneRules prune)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var collectors = new List<TreeCollector>();
        var started = Stopwatch.GetTimestamp();
        new FastWalker(prune).Walk(normalized, () =>
        {
            var collector = new TreeCollector();
            lock (collectors)
            {
                collectors.Add(collector);
            }
            return collector.Visit;
        }, Deadline.After(TimeSpan.MaxValue, started));
        return new RootWalk(normalized, collectors, Stopwatch.GetElapsedTime(started));
    }

    FileStream AcquireLock()
    {
        try
        {
            // FileShare.None is a real exclusive lock on windows and an flock on unix; the os drops it if we die
            return new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, bufferSize: 1);
        }
        catch (IOException)
        {
            throw new IndexBusyException(LockPath);
        }
    }

    void Step(RebuildStep step) => BeforeStep?.Invoke(step);
}

enum RebuildStep
{
    Walk,
    WriteTemp,
    Flush,
    Rename,
    Done,
}
