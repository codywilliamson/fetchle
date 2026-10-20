using BenchmarkDotNet.Attributes;
using Fetchle.Core.Index;

namespace Fetchle.Bench;

// a rebuild minus the walk, one phase at a time: rows and interning, the image, its checks, the file swap
[MemoryDiagnoser]
public class RebuildBenchmarks
{
    RootWalk _walk = null!;
    PathTree _tree = null!;
    byte[] _image = [];
    IndexStore _store = null!;

    [Params(50_000, 1_000_000)]
    public int Entries { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _walk = SyntheticTree.Collect(Entries);
        _tree = TreeAssembler.Assemble([_walk]);
        _image = SnapshotWriter.ToBytes(_tree, DateTimeOffset.UnixEpoch);
        _store = new IndexStore(Path.Combine(Path.GetTempPath(), $"fetchle-bench-rebuild-{Entries}"));
        Directory.CreateDirectory(_store.Directory);
    }

    [Benchmark]
    public int Assemble() => TreeAssembler.Assemble([_walk]).EntryCount;

    [Benchmark]
    public int Image() => SnapshotWriter.ToBytes(_tree, DateTimeOffset.UnixEpoch).Length;

    [Benchmark]
    public int Check()
    {
        using var snapshot = Snapshot.FromBytes(_image);
        return snapshot.EntryCount;
    }

    [Benchmark]
    public long WriteAndSwap() => _store.WriteAndSwap(_image);
}
