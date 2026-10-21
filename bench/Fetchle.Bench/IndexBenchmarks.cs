using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Fetchle.Core.Index;
using Fetchle.Core.Ranking;
using Fetchle.Core.Search;

namespace Fetchle.Bench;

// the snapshot side of a query: open (map + validate) and the lexical ranked pass. v0.1 ran the substring pass
// under the same name and params, so numbers compare across the change
[MemoryDiagnoser]
public class IndexBenchmarks
{
    PathTree _tree = null!;
    string _file = "";
    Snapshot _snapshot = null!;
    SearchRequest _request = null!;
    IndexScope[] _scopes = [];

    [Params(50_000, 1_000_000)]
    public int Entries { get; set; }

    // "settings" hits a handful, ".json" hits thousands, "zzz" hits nothing
    [Params("settings", ".json", "zzz")]
    public string Query { get; set; } = "";

    [GlobalSetup]
    public void Setup()
    {
        _tree = SyntheticTree.Build(Entries);
        _file = Path.Combine(Path.GetTempPath(), $"fetchle-bench-{Entries}.fidx");
        File.WriteAllBytes(_file, SnapshotWriter.ToBytes(_tree, DateTimeOffset.UnixEpoch));
        _snapshot = Snapshot.Open(_file)!;
        _scopes = [new IndexScope(0, SyntheticTree.Root)];
        _request = new SearchRequest(Query, [SyntheticTree.Root], SearchRequest.DEFAULT_LIMIT, TimeSpan.FromMinutes(5));
    }

    [GlobalCleanup]
    public void Cleanup() => _snapshot.Dispose();

    [Benchmark]
    public int Open()
    {
        using var snapshot = Snapshot.Open(_file)!;
        return snapshot.EntryCount;
    }

    [Benchmark]
    public int Search() =>
        RankedQuery.Run(_snapshot, _request, QueryTerms.Parse(Query), _scopes, Deadline.After(TimeSpan.FromMinutes(5), Stopwatch.GetTimestamp())).Total;
}
