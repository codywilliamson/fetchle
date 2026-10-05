using BenchmarkDotNet.Attributes;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;

namespace Fetchle.Bench;

[MemoryDiagnoser]
public class QueryBenchmarks
{
    readonly IFileSearch _search = new FileSearch(PruneRules.Default);
    SearchRequest _request = null!;

    [ParamsSource(nameof(Shapes))]
    public string Shape { get; set; } = BenchCorpus.REALISTIC;

    public static IEnumerable<string> Shapes => BenchCorpus.Shapes;

    // "settings" hits a handful of landmarks, ".json" hits thousands, "zzz" hits nothing
    [Params("settings", ".json", "zzz")]
    public string Query { get; set; } = "";

    [GlobalSetup]
    public void Setup() =>
        _request = new SearchRequest(Query, [BenchCorpus.Ensure(Shape)], SearchRequest.DEFAULT_LIMIT, TimeSpan.FromMinutes(5));

    [Benchmark]
    public int Search() => _search.Search(_request, CancellationToken.None).TotalMatches;
}
