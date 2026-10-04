using Fetchle.Core.Search;
using Fetchle.Evals.Queries;
using Fetchle.Evals.Reporting;

namespace Fetchle.Evals.Running;

public sealed class EvalRunner(IFileSearch search, string corpus, TextWriter output)
{
    const int LIMIT = 10;
    static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    public List<EvalQueryResult> Run(IEnumerable<EvalQuery> queries)
    {
        var results = new List<EvalQueryResult>();
        foreach (var query in queries)
        {
            var result = search.Search(new SearchRequest(query.Query, [corpus], LIMIT, Budget), CancellationToken.None);
            var rank = FindRank(result, query);
            results.Add(new EvalQueryResult(query.Query, rank, result.TotalMatches, (long)result.Elapsed.TotalMilliseconds));

            var rankLabel = rank is { } found ? $"#{found}" : "-";
            output.WriteLine($"{rankLabel,4}  {result.TotalMatches,6} matches  {query.Query}");
        }
        return results;
    }

    int? FindRank(SearchResult result, EvalQuery query)
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
}
