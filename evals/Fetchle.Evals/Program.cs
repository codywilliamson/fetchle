using Fetchle.Evals;
using Fetchle.Evals.Corpus;
using Fetchle.Evals.Logging;
using Fetchle.Evals.Queries;
using Fetchle.Evals.Reporting;
using Fetchle.Evals.Running;
using Microsoft.Extensions.Logging;

// usage: Fetchle.Evals [--split dev|heldout] [--gate] [--update-baseline] [--lexical-only | --semantic-only]
//   dev is the default and the only split anyone tunes on. --gate and --update-baseline always run held-out:
//   ci gates on held-out against evals/baseline.json, and the baseline only ever holds held-out numbers

using var loggerFactory = EvalsLog.CreateFactory();
var logger = loggerFactory.CreateLogger("Fetchle.Evals");

var gate = args.Contains("--gate");
var updateBaseline = args.Contains("--update-baseline");
var split = gate || updateBaseline || Arg("--split") == "heldout" ? Split.HeldOut : Split.Dev;

var repo = RepoRoot.Find();
var modelPath = Environment.GetEnvironmentVariable(Fetchle.Core.Embedding.StaticModel.ENV_PATH) is { Length: > 0 } fromEnv
    ? (Directory.Exists(fromEnv) ? Path.Combine(fromEnv, Fetchle.Core.Embedding.StaticModel.FILE_NAME) : fromEnv)
    : Path.Combine(repo, "artifacts", "model", Fetchle.Core.Embedding.StaticModel.FILE_NAME);
using var corpus = EvalCorpus.Build(Path.Combine(repo, "evals", "corpus", "paths.txt"), modelPath);
Console.WriteLine(corpus.Describe());
var (scored, setAside) = QuerySet.Load(Path.Combine(repo, "evals", "queries"), corpus.Paths, EvalCorpus.IsPruned);
var queries = scored.Where(q => QuerySplit.Of(q) == split).ToList();
var prunedInSplit = setAside.Count(q => QuerySplit.Of(q) == split);

// --lexical-only and --semantic-only are for diagnosis: which retriever lost a query
IEvalSearcher searcher = new RankedSearcher(lexicalOnly: args.Contains("--lexical-only"), semanticOnly: args.Contains("--semantic-only"));

var results = new EvalRunner(searcher, corpus, Console.Out).Run(queries);
var splitName = split == Split.HeldOut ? "heldout" : "dev";
var report = EvalReport.From(searcher.Name, splitName, prunedInSplit, results);
Console.WriteLine($"{splitName}: top-1 {report.Top1:P1}  top-3 {report.Top3:P1}  mrr {report.Mrr:F3}  ({results.Count} queries, {prunedInSplit} set aside under pruned dirs)");

var store = new ReportStore(
    Path.Combine(repo, "artifacts", "evals", $"results-{splitName}.json"),
    Path.Combine(repo, "evals", "baseline.json"));
store.WriteResults(report);
logger.WroteResults(store.ResultsPath);

if (updateBaseline)
{
    store.WriteBaseline(report);
    logger.UpdatedBaseline(store.BaselinePath);
    return 0;
}

if (gate)
{
    var baseline = store.ReadBaseline();
    if (report.RegressedFrom(baseline))
    {
        logger.RankingRegressed(report.Top1, report.Top3, report.Mrr, baseline.Top1, baseline.Top3, baseline.Mrr);
        return 1;
    }
}
return 0;

string? Arg(string name)
{
    var at = Array.IndexOf(args, name);
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}
