using System.Diagnostics;
using Fetchle.Core.Ranking.Semantic;
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
    const int LOCK_RETRY_MS = 50;

    public string Directory { get; } = directory;

    public string SnapshotPath => Path.Combine(Directory, SNAPSHOT_FILE);

    public string TempPath => SnapshotPath + TEMP_SUFFIX;

    public string LockPath => Path.Combine(Directory, LOCK_FILE);

    public string VectorsPath => Path.Combine(Directory, Ranking.Semantic.VectorFormat.FILE_NAME);

    // how long a rebuild waits for another writer before giving up
    internal TimeSpan LockWait { get; init; } = TimeSpan.FromSeconds(3);

    // crash tests stop a rebuild between steps here
    internal Action<RebuildStep>? BeforeStep { get; init; }

    // null when there's no snapshot. throws IndexUnreadableException when there is one but it can't be trusted
    public SnapshotInfo? ReadInfo()
    {
        using var snapshot = Snapshot.Open(SnapshotPath);
        return snapshot?.Info();
    }

    internal Snapshot? Open() => Snapshot.Open(SnapshotPath);

    // walks every root and swaps in a new snapshot, then, with a model, its vectors. the snapshot lands first, so
    // lexical search answers from the new tree while segments embed. throws IndexBusyException when another writer
    // holds the lock
    public RebuildReport Rebuild(IReadOnlyList<string> roots, PruneRules prune, CancellationToken cancellationToken) =>
        Rebuild(roots, prune, null, cancellationToken);

    public RebuildReport Rebuild(IReadOnlyList<string> roots, PruneRules prune, ITextEncoder? model, CancellationToken cancellationToken)
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
            var (walk, _) = WalkRoot(root, prune, Deadline.After(TimeSpan.MaxValue, Stopwatch.GetTimestamp()));
            walkTime += walk.WalkTime;
            walks.Add(walk);
        }

        var built = Stopwatch.GetTimestamp();
        var tree = TreeAssembler.Assemble(walks);
        var image = SnapshotWriter.ToBytes(tree, DateTimeOffset.UtcNow);
        // structure checked before anything touches disk, so a writer bug can never replace a good snapshot with a
        // bad one. the checksum was computed a moment ago from these same bytes, so it isn't hashed twice
        using var written = Snapshot.FromWrittenImage(image, SnapshotPath);
        cancellationToken.ThrowIfCancellationRequested();

        var buildTime = Stopwatch.GetElapsedTime(built);
        var writing = Stopwatch.GetTimestamp();
        WriteAndSwap(image, TempPath, SnapshotPath);
        var writeTime = Stopwatch.GetElapsedTime(writing);

        TimeSpan? embedTime = null;
        long vectorBytes = 0;
        if (SignCodes.Fits(model))
        {
            var embedding = Stopwatch.GetTimestamp();
            var vectors = VectorWriter.ToBytes(VectorBuilder.Build(written, model!, cancellationToken), written.Checksum, model!.Checksum);
            vectorBytes = WriteAndSwap(vectors, VectorsPath + TEMP_SUFFIX, VectorsPath);
            embedTime = Stopwatch.GetElapsedTime(embedding);
        }

        Step(RebuildStep.Done);
        return new RebuildReport(written.Info(), walkTime, buildTime, writeTime, Stopwatch.GetElapsedTime(start), embedTime, vectorBytes);
    }

    // temp file, flushed, then renamed over the target. returns bytes written
    internal long WriteAndSwap(byte[] image, string tempPath, string targetPath)
    {
        try
        {
            Step(RebuildStep.WriteTemp);
            using (var temp = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1))
            {
                temp.Write(image);
                Step(RebuildStep.Flush);
                temp.Flush(flushToDisk: true);
            }

            Step(RebuildStep.Rename);
            AtomicReplace.Move(tempPath, targetPath);
            return image.Length;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a snapshot-sized temp file isn't worth leaving behind; readers never look at it either way
            TryDelete(tempPath);
            throw;
        }
    }

    static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    // the walk and whether it finished before the deadline
    internal static (RootWalk Walk, bool Completed) WalkRoot(string root, PruneRules prune, Deadline deadline)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var collectors = new List<TreeCollector>();
        var started = Stopwatch.GetTimestamp();
        var completed = new FastWalker(prune).Walk(normalized, () =>
        {
            var collector = new TreeCollector();
            lock (collectors)
            {
                collectors.Add(collector);
            }
            return collector.Visit;
        }, deadline);
        return (new RootWalk(normalized, collectors, Stopwatch.GetElapsedTime(started)), completed);
    }

    // a writer that just died can hold its handle a moment past exit on windows, and a second rebuild started
    // right after the first should wait it out rather than fail, so this retries for a few seconds
    FileStream AcquireLock()
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                // FileShare.None is a real exclusive lock on windows and an flock on unix; the os drops it if we die
                return new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, bufferSize: 1);
            }
            catch (IOException) when (waited.Elapsed < LockWait)
            {
                Thread.Sleep(LOCK_RETRY_MS);
            }
            catch (IOException)
            {
                throw new IndexBusyException(LockPath);
            }
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
