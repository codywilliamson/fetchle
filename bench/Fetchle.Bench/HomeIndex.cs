using Fetchle.Core.Index;
using Fetchle.Core.Ranking.Semantic;
using Fetchle.Fixtures;

namespace Fetchle.Bench;

// the 1M-entry home corpus as a built index (snapshot + vectors from the real model) in a temp dir, made once per
// machine and mapped on later runs like the real thing. the corpus never touches disk, only the index does
static class HomeIndex
{
    // bump when the snapshot or vectors format changes
    const string VERSION = "v3";

    // searches resolve their root on disk even though the snapshot answers them, so the dir exists and stays empty
    public static readonly string Root = Path.Join(Path.GetTempPath(), "fetchle-home-index-root");

    public static IndexStore Ensure(int entries)
    {
        Directory.CreateDirectory(Root);
        var store = new IndexStore(Path.Join(Path.GetTempPath(), $"fetchle-home-index-{entries}-{VERSION}"));
        if (File.Exists(store.SnapshotPath) && File.Exists(store.VectorsPath))
        {
            return store;
        }

        Directory.CreateDirectory(store.Directory);
        var image = SnapshotWriter.ToBytes(TreeFromPaths.Build(Root, HomeCorpus.Entries(entries)), DateTimeOffset.UnixEpoch);
        store.WriteAndSwap(image, store.TempPath, store.SnapshotPath);
        using var snapshot = Snapshot.FromBytes(image);
        var model = BenchModel.Instance;
        var vectors = VectorWriter.ToBytes(VectorBuilder.Build(snapshot, model), snapshot.Checksum, model.Checksum);
        store.WriteAndSwap(vectors, store.VectorsPath + IndexStore.TEMP_SUFFIX, store.VectorsPath);
        return store;
    }
}
