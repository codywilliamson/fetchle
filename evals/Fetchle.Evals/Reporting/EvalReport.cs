namespace Fetchle.Evals.Reporting;

public sealed record EvalQueryResult(string Query, int? Rank, int TotalMatches, long ElapsedMs);

public sealed record EvalReport(string Searcher, int CorpusFiller, double Top1, double Top3, List<EvalQueryResult> Queries)
{
    public static EvalReport From(string searcher, int corpusFiller, List<EvalQueryResult> results) =>
        new(searcher, corpusFiller, HitRate(results, 1), HitRate(results, 3), results);

    public bool RegressedFrom(EvalReport baseline) => Top1 < baseline.Top1 || Top3 < baseline.Top3;

    static double HitRate(List<EvalQueryResult> results, int k)
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
}
