namespace Fetchle.Core.Search;

static class EntryStat
{
    // a file that vanished mid-walk, or a path the os can't even parse, reads as empty and old rather than throwing
    public static (long? Size, DateTimeOffset Modified) Read(string path, bool isDirectory)
    {
        FileSystemInfo info;
        try
        {
            info = isDirectory ? new DirectoryInfo(path) : new FileInfo(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Missing(isDirectory);
        }

        if (!info.Exists)
        {
            return Missing(isDirectory);
        }
        return (info is FileInfo file ? file.Length : null, info.LastWriteTimeUtc);
    }

    static (long? Size, DateTimeOffset Modified) Missing(bool isDirectory) => (isDirectory ? null : 0, DateTimeOffset.UnixEpoch);
}
