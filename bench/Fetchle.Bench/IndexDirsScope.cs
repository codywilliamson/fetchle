namespace Fetchle.Bench;

// two throwaway fetchle data dirs, deleted on dispose
public sealed class IndexDirsScope : IDisposable
{
    readonly string _parent = Path.Combine(Path.GetTempPath(), $"fetchle-bench-data-{Guid.NewGuid():N}");

    public IndexDirs Dirs => new(Path.Combine(_parent, "query"), Path.Combine(_parent, "rebuild"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_parent, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
