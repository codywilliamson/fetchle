using Fetchle.Core.Walking;

namespace Fetchle.Core.Index;

// the index directory: one snapshot file, its temp sibling while a rebuild writes, and the writer lock
public sealed class IndexStore(string directory)
{
    public const string SNAPSHOT_FILE = "snapshot.fidx";
    public const string TEMP_SUFFIX = ".tmp";
    public const string LOCK_FILE = "write.lock";

    public string Directory { get; } = directory;

    public string SnapshotPath => Path.Combine(Directory, SNAPSHOT_FILE);

    public string LockPath => Path.Combine(Directory, LOCK_FILE);

    // null when there's no snapshot. throws IndexUnreadableException when there is one but it can't be trusted
    public SnapshotInfo? ReadInfo() => throw new NotImplementedException();

    // walks every root and swaps in a new snapshot. throws IndexBusyException when another writer holds the lock
    public RebuildReport Rebuild(IReadOnlyList<string> roots, PruneRules prune, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
