namespace Fetchle.Core.Index;

public sealed record SnapshotRoot(string Path, int Entries, TimeSpan WalkTime);

public sealed record SnapshotInfo(string File, DateTimeOffset BuiltAt, int Entries, int Segments, long Bytes, IReadOnlyList<SnapshotRoot> Roots)
{
    public double BytesPerEntry => Entries == 0 ? 0 : (double)Bytes / Entries;
}

public sealed record RebuildReport(SnapshotInfo Snapshot, TimeSpan WalkTime, TimeSpan TotalTime);
