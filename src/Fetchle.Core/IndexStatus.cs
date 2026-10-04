namespace Fetchle.Core;

public sealed record RootStatus(string Path, long? FileCount, DateTimeOffset? LastFullScan);

public sealed record IndexStatus(IReadOnlyList<RootStatus> Roots, bool VectorsComplete, bool WatcherLive);
