using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Fetchle.Core.Index;
using Fetchle.Core.Ranking;
using Fetchle.Core.Ranking.Semantic;
using Fetchle.Core.Search;
using Fetchle.Fixtures;

namespace Fetchle.Bench;

// the v0.2 ranked query on the 1M-entry home corpus with the real model: per-segment term scores and semantic scores,
// the entry pass, fusion. plus opening the vectors, the other thing a CLI query pays for
[MemoryDiagnoser]
public class RankedBenchmarks
{
    Snapshot _snapshot = null!;
    SegmentVectors _vectors = null!;
    IndexStore _store = null!;
    SearchRequest _request = null!;
    IndexScope[] _scopes = [];

    // an exact name, three words both retrievers see, and words lexical can't find
    [Params("settings", "windows terminal settings", "music streaming app")]
    public string Query { get; set; } = "";

    [GlobalSetup]
    public void Setup()
    {
        _store = HomeIndex.Ensure(HomeCorpus.DEFAULT_ENTRIES);
        _snapshot = Snapshot.Open(_store.SnapshotPath)!;
        _vectors = SegmentVectors.Open(_store.VectorsPath, _snapshot, BenchModel.Instance.Checksum, out var problem)
            ?? throw new InvalidOperationException(problem);
        _scopes = [new IndexScope(0, HomeIndex.Root)];
        _request = new SearchRequest(Query, [HomeIndex.Root], SearchRequest.DEFAULT_LIMIT, TimeSpan.FromMinutes(5));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _vectors.Dispose();
        _snapshot.Dispose();
    }

    [Benchmark]
    public int Search()
    {
        using var semantic = SemanticScores.Build(_snapshot, _vectors, BenchModel.Instance, Query);
        var outcome = RankedQuery.Run(_snapshot, _request, QueryTerms.Parse(Query), _scopes, Deadline.After(TimeSpan.FromMinutes(5), Stopwatch.GetTimestamp()), semantic);
        return Fusion.Fuse([(_snapshot, outcome)], SearchRequest.DEFAULT_LIMIT).Count;
    }

    [Benchmark]
    public int OpenVectors()
    {
        using var vectors = SegmentVectors.Open(_store.VectorsPath, _snapshot, BenchModel.Instance.Checksum, out _)!;
        return vectors.Norms.Length;
    }
}

// the background embed after a rebuild: every unique segment of the 1M-entry home corpus, then every path's centroid
// length. the v0.2 target is under 10 s
[MemoryDiagnoser]
public class EmbedBenchmarks
{
    Snapshot _snapshot = null!;

    [GlobalSetup]
    public void Setup() => _snapshot = Snapshot.Open(HomeIndex.Ensure(HomeCorpus.DEFAULT_ENTRIES).SnapshotPath)!;

    [GlobalCleanup]
    public void Cleanup() => _snapshot.Dispose();

    [Benchmark]
    public int Embed() => VectorBuilder.Build(_snapshot, BenchModel.Instance).Norms.Length;
}
