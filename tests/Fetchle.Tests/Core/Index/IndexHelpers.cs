using Fetchle.Core.Index;
using Fetchle.Core.Walking;

namespace Fetchle.Tests.Core.Index;

// a throwaway index directory with a snapshot built from real walks
sealed class TempIndex : IDisposable
{
    public TempIndex()
    {
        Directory.CreateDirectory(Dir);
        Store = new IndexStore(Dir);
    }

    public string Dir { get; } = Path.Combine(Path.GetTempPath(), $"fetchle-index-test-{Guid.NewGuid():N}");

    public IndexStore Store { get; }

    public static TempIndex Built(params string[] roots)
    {
        var index = new TempIndex();
        index.Store.Rebuild(roots, PruneRules.Default, CancellationToken.None);
        return index;
    }

    public byte[] SnapshotBytes() => File.ReadAllBytes(Store.SnapshotPath);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
