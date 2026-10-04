using System.Text.Json;
using System.Text.Json.Serialization;
using Fetchle.Core.Naive;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;
using Fetchle.Fixtures;
using Microsoft.Extensions.Logging;

// usage: Fetchle.Evals [--update-baseline]

const int CORPUS_FILLER = 20_000;
const int LIMIT = 10;

using var loggerFactory = CreateLoggerFactory();
var logger = loggerFactory.CreateLogger("Fetchle.Evals");

var repo = RepoRoot();
var queriesJson = File.ReadAllText(Path.Combine(repo, "evals", "queries.json"));
var queries = JsonSerializer.Deserialize(queriesJson, EvalJson.Default.EvalQueryArray)!;
var corpus = Corpus.Ensure(Corpus.DefaultRoot(CORPUS_FILLER), CORPUS_FILLER);
EnsureExpectedFilesExist(queries, corpus);

IFileSearch search = new NaiveFileSearch(PruneRules.Default);
var results = new List<EvalQueryResult>();
foreach (var query in queries)
{
    var request = new SearchRequest(query.Query, [corpus], LIMIT, TimeSpan.FromSeconds(30));
    var result = search.Search(request, CancellationToken.None);
    var rank = FindRank(result, query, corpus);
    results.Add(new EvalQueryResult(query.Query, rank, result.TotalMatches, (long)result.Elapsed.TotalMilliseconds));

    var rankLabel = rank is { } found ? $"#{found}" : "-";
    Console.WriteLine($"{rankLabel,4}  {result.TotalMatches,6} matches  {query.Query}");
}

var top1 = Rate(results, 1);
var top3 = Rate(results, 3);
var report = new EvalReport("NaiveFileSearch", CORPUS_FILLER, top1, top3, results);
Console.WriteLine($"top-1 {top1:P0}  top-3 {top3:P0}  ({results.Count} queries)");

var resultsPath = Path.Combine(repo, "artifacts", "evals", "results.json");
Directory.CreateDirectory(Path.GetDirectoryName(resultsPath)!);
File.WriteAllText(resultsPath, JsonSerializer.Serialize(report, EvalJson.Default.EvalReport));
logger.WroteResults(resultsPath);

var baselinePath = Path.Combine(repo, "evals", "baseline.json");
if (args is ["--update-baseline"])
{
    File.WriteAllText(baselinePath, JsonSerializer.Serialize(report, EvalJson.Default.EvalReport) + "\n");
    logger.UpdatedBaseline(baselinePath);
    return 0;
}

var baseline = JsonSerializer.Deserialize(File.ReadAllText(baselinePath), EvalJson.Default.EvalReport)!;
if (top1 < baseline.Top1 || top3 < baseline.Top3)
{
    logger.RankingRegressed(top1, top3, baseline.Top1, baseline.Top3);
    return 1;
}
return 0;

static ILoggerFactory CreateLoggerFactory()
{
    return LoggerFactory.Create(builder =>
    {
        // stdout carries the eval table, so log lines go to stderr
        builder.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
    });
}

static void EnsureExpectedFilesExist(EvalQuery[] queries, string corpus)
{
    foreach (var query in queries)
    {
        foreach (var expected in query.Expected)
        {
            if (!File.Exists(Path.Combine(corpus, expected)))
            {
                throw new InvalidOperationException($"'{query.Query}' expects {expected}, which the corpus doesn't create");
            }
        }
    }
}

static int? FindRank(SearchResult result, EvalQuery query, string corpus)
{
    for (var i = 0; i < result.Hits.Count; i++)
    {
        var relative = Path.GetRelativePath(corpus, result.Hits[i].Path).Replace('\\', '/');
        if (query.Expected.Contains(relative))
        {
            return i + 1;
        }
    }
    return null;
}

static double Rate(List<EvalQueryResult> results, int k)
{
    if (results.Count == 0)
    {
        return 0;
    }

    var hits = 0;
    foreach (var result in results)
    {
        if (result.Rank <= k)
        {
            hits++;
        }
    }
    return (double)hits / results.Count;
}

static string RepoRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "fetchle.slnx")))
        {
            return dir.FullName;
        }
    }
    throw new InvalidOperationException("can't find fetchle.slnx above " + AppContext.BaseDirectory);
}

sealed record EvalQuery(string Query, string[] Expected);

sealed record EvalQueryResult(string Query, int? Rank, int TotalMatches, long ElapsedMs);

sealed record EvalReport(string Searcher, int CorpusFiller, double Top1, double Top3, List<EvalQueryResult> Queries);

[JsonSerializable(typeof(EvalQuery[]))]
[JsonSerializable(typeof(EvalReport))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = true)]
partial class EvalJson : JsonSerializerContext;

static partial class EvalsLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "wrote {Path}")]
    public static partial void WroteResults(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "updated {Path}")]
    public static partial void UpdatedBaseline(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "ranking regressed: top-1 {Top1:P0} top-3 {Top3:P0}, baseline top-1 {BaselineTop1:P0} top-3 {BaselineTop3:P0}")]
    public static partial void RankingRegressed(this ILogger logger, double top1, double top3, double baselineTop1, double baselineTop3);
}
