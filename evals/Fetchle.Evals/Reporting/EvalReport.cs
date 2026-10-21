namespace Fetchle.Evals.Reporting;

public sealed record EvalQueryResult(string Id, string Query, string Kind, int? Rank, int TotalMatches, double ElapsedMs, List<string> Top);

public sealed record EvalReport(string Searcher, string Split, int Queries, int SetAside, double Top1, double Top3, double Mrr, List<EvalQueryResult> Results)
{
    public static EvalReport From(string searcher, string split, int setAside, List<EvalQueryResult> results) =>
        new(searcher, split, results.Count, setAside, HitRate(results, 1), HitRate(results, 3), MeanReciprocalRank(results), results);

    // any of the three below the committed baseline fails the gate
    public bool RegressedFrom(EvalReport baseline) => Top1 < baseline.Top1 || Top3 < baseline.Top3 || Mrr < baseline.Mrr;

    static double HitRate(List<EvalQueryResult> results, int k) =>
        results.Count == 0 ? 0 : (double)results.Count(r => r.Rank <= k) / results.Count;

    // mean reciprocal rank inside the top 10, a miss counts 0
    static double MeanReciprocalRank(List<EvalQueryResult> results) =>
        results.Count == 0 ? 0 : results.Sum(r => r.Rank is { } rank ? 1.0 / rank : 0) / results.Count;
}
