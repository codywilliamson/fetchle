namespace Fetchle.Core.Index;

// the snapshot exists but can't be trusted: truncated, corrupt, wrong version or wrong endianness. exit 3
public sealed class IndexUnreadableException(string path, string reason) : Exception($"index unreadable ({reason}): {path}")
{
    public string Path { get; } = path;

    public string Reason { get; } = reason;
}

// another process holds the write lock
public sealed class IndexBusyException(string lockPath) : Exception($"another index build holds {lockPath}")
{
    public string LockPath { get; } = lockPath;
}
