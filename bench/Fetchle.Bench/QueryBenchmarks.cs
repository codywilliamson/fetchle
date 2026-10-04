using BenchmarkDotNet.Attributes;
using Fetchle.Core.Naive;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;

namespace Fetchle.Bench;

[MemoryDiagnoser]
public class QueryBenchmarks
{
    readonly IFileSearch _search = new NaiveFileSearch(PruneRules.Default);
    SearchRequest _request = null!;

    [ParamsSource(nameof(Shapes))]
    public string Shape { get; set; } = BenchCorpus.Realistic;

    public static IEnumerable<string> Shapes => BenchCorpus.Shapes;

    // "settings" hits a handful of landmarks, "zzz" hits nothing and costs a full walk
    [Params("settings", "zzz")]
    public string Query { get; set; } = "";

    [GlobalSetup]
    public void Setup() =>
        _request = new SearchRequest(Query, [BenchCorpus.Ensure(Shape)], SearchRequest.DefaultLimit, TimeSpan.FromMinutes(5));

    [Benchmark]
    public int NaiveSearch() => _search.Search(_request, CancellationToken.None).TotalMatches;
}
