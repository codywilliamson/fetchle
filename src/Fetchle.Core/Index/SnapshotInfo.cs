namespace Fetchle.Core.Index;

// Entries counts files and dirs below the root, Files only the files
public sealed record SnapshotRoot(string Path, int Entries, int Files, TimeSpan WalkTime);

public sealed record SnapshotInfo(string File, DateTimeOffset BuiltAt, int Entries, int Segments, long Bytes, IReadOnlyList<SnapshotRoot> Roots)
{
    public double BytesPerEntry => Entries == 0 ? 0 : (double)Bytes / Entries;
}

// walk: listing the roots. build: rows, interning, the image and its checks. write: temp file, flush, rename
public sealed record RebuildReport(SnapshotInfo Snapshot, TimeSpan WalkTime, TimeSpan BuildTime, TimeSpan WriteTime, TimeSpan TotalTime);
