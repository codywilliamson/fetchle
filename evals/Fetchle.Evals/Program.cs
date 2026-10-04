using Fetchle.Core.Naive;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;
using Fetchle.Evals;
using Fetchle.Evals.Logging;
using Fetchle.Evals.Queries;
using Fetchle.Evals.Reporting;
using Fetchle.Evals.Running;
using Fetchle.Fixtures;
using Microsoft.Extensions.Logging;

// usage: Fetchle.Evals [--update-baseline]

const int CORPUS_FILLER = 20_000;

using var loggerFactory = EvalsLog.CreateFactory();
var logger = loggerFactory.CreateLogger("Fetchle.Evals");

var repo = RepoRoot.Find();
var corpus = Corpus.Ensure(Corpus.DefaultRoot(CORPUS_FILLER), CORPUS_FILLER);
var queries = QuerySet.Load(Path.Combine(repo, "evals", "queries.json"), corpus);
var store = new ReportStore(
    Path.Combine(repo, "artifacts", "evals", "results.json"),
    Path.Combine(repo, "evals", "baseline.json"));

IFileSearch search = new NaiveFileSearch(PruneRules.Default);
var results = new EvalRunner(search, corpus, Console.Out).Run(queries);
var report = EvalReport.From(nameof(NaiveFileSearch), CORPUS_FILLER, results);
Console.WriteLine($"top-1 {report.Top1:P0}  top-3 {report.Top3:P0}  ({results.Count} queries)");

store.WriteResults(report);
logger.WroteResults(store.ResultsPath);

if (args is ["--update-baseline"])
{
    store.WriteBaseline(report);
    logger.UpdatedBaseline(store.BaselinePath);
    return 0;
}

var baseline = store.ReadBaseline();
if (report.RegressedFrom(baseline))
{
    logger.RankingRegressed(report.Top1, report.Top3, baseline.Top1, baseline.Top3);
    return 1;
}
return 0;
