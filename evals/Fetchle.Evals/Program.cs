using System.Text.Json;
using System.Text.Json.Serialization;
using Fetchle.Core.Naive;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;
using Fetchle.Fixtures;

// ranking quality gate, docs/testing.md#evals-are-not-tests.
// usage: Fetchle.Evals [--update-baseline]
// runs evals/queries.json over the generated corpus, writes artifacts/evals/results.json,
// and fails when top-1 or top-3 drops below evals/baseline.json

const int CORPUS_FILLER = 20_000;
const int LIMIT = 10;

var repo = RepoRoot();
var queries = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(repo, "evals", "queries.json")), EvalJson.Default.EvalQueryArray)!;
var corpus = Corpus.Ensure(Corpus.DefaultRoot(CORPUS_FILLER), CORPUS_FILLER);

foreach (var q in queries)
    foreach (var expected in q.Expected)
        if (!File.Exists(Path.Combine(corpus, expected)))
            throw new InvalidOperationException($"'{q.Query}' expects {expected}, which the corpus doesn't create");

IFileSearch search = new NaiveFileSearch(PruneRules.Default);
var results = new List<EvalQueryResult>();
foreach (var q in queries)
{
    var result = search.Search(new SearchRequest(q.Query, [corpus], LIMIT, TimeSpan.FromSeconds(30)), CancellationToken.None);
    int? rank = null;
    for (var i = 0; i < result.Hits.Count && rank is null; i++)
    {
        var relative = Path.GetRelativePath(corpus, result.Hits[i].Path).Replace('\\', '/');
        if (q.Expected.Contains(relative)) rank = i + 1;
    }
    results.Add(new EvalQueryResult(q.Query, rank, result.TotalMatches, (long)result.Elapsed.TotalMilliseconds));
    Console.WriteLine($"{(rank is { } r ? $"#{r}" : "-"),4}  {result.TotalMatches,6} matches  {q.Query}");
}

var top1 = Rate(results, 1);
var top3 = Rate(results, 3);
var report = new EvalReport("NaiveFileSearch", CORPUS_FILLER, top1, top3, results);
Console.WriteLine($"top-1 {top1:P0}  top-3 {top3:P0}  ({results.Count} queries)");

var resultsPath = Path.Combine(repo, "artifacts", "evals", "results.json");
Directory.CreateDirectory(Path.GetDirectoryName(resultsPath)!);
File.WriteAllText(resultsPath, JsonSerializer.Serialize(report, EvalJson.Default.EvalReport));
Console.WriteLine($"wrote {resultsPath}");

var baselinePath = Path.Combine(repo, "evals", "baseline.json");
if (args is ["--update-baseline"])
{
    File.WriteAllText(baselinePath, JsonSerializer.Serialize(report, EvalJson.Default.EvalReport) + "\n");
    Console.WriteLine($"updated {baselinePath}");
    return 0;
}
var baseline = JsonSerializer.Deserialize(File.ReadAllText(baselinePath), EvalJson.Default.EvalReport)!;
if (top1 < baseline.Top1 || top3 < baseline.Top3)
{
    Console.Error.WriteLine($"ranking regressed: baseline top-1 {baseline.Top1:P0} top-3 {baseline.Top3:P0}");
    return 1;
}
return 0;

static double Rate(List<EvalQueryResult> results, int k)
{
    var hits = 0;
    foreach (var r in results) if (r.Rank <= k) hits++;
    return results.Count == 0 ? 0 : (double)hits / results.Count;
}

static string RepoRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "fetchle.slnx"))) return dir.FullName;
    throw new InvalidOperationException("can't find fetchle.slnx above " + AppContext.BaseDirectory);
}

sealed record EvalQuery(string Query, string[] Expected);

sealed record EvalQueryResult(string Query, int? Rank, int TotalMatches, long ElapsedMs);

sealed record EvalReport(string Searcher, int CorpusFiller, double Top1, double Top3, List<EvalQueryResult> Queries);

[JsonSerializable(typeof(EvalQuery[]))]
[JsonSerializable(typeof(EvalReport))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = true)]
partial class EvalJson : JsonSerializerContext;
