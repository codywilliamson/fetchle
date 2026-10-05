namespace Fetchle.Core.Search;

static class EntryStat
{
    // a file that vanished mid-walk reads as empty and old rather than throwing
    public static (long? Size, DateTimeOffset Modified) Read(string path, bool isDirectory)
    {
        FileSystemInfo info = isDirectory ? new DirectoryInfo(path) : new FileInfo(path);
        if (!info.Exists)
        {
            return (isDirectory ? null : 0, DateTimeOffset.UnixEpoch);
        }
        return (info is FileInfo file ? file.Length : null, info.LastWriteTimeUtc);
    }
}
