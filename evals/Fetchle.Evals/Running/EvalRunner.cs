using Fetchle.Evals.Corpus;
using Fetchle.Evals.Queries;
using Fetchle.Evals.Reporting;

namespace Fetchle.Evals.Running;

public sealed class EvalRunner(IEvalSearcher searcher, EvalCorpus corpus, TextWriter output)
{
    public const int LIMIT = 10;

    public List<EvalQueryResult> Run(IEnumerable<EvalQuery> queries)
    {
        var results = new List<EvalQueryResult>();
        foreach (var query in queries)
        {
            var result = searcher.Search(corpus, query.Query, LIMIT);
            var top = new List<string>(result.Hits.Count);
            int? rank = null;
            foreach (var hit in result.Hits)
            {
                var relative = Path.GetRelativePath(corpus.Root, hit.Path).Replace('\\', '/');
                top.Add(relative);
                if (rank is null && query.Expected.Any(e => e.TrimEnd('/') == relative))
                {
                    rank = top.Count;
                }
            }

            results.Add(new EvalQueryResult(query.Id, query.Query, query.Kind, rank, result.TotalMatches, result.Elapsed.TotalMilliseconds, top));
            output.WriteLine($"{(rank is { } r ? $"#{r}" : "-"),4}  {query.Id,-6} {query.Query}");
        }
        return results;
    }
}
