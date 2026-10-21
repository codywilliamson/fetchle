using System.Text.Json;
using Fetchle.Evals.Reporting;

namespace Fetchle.Evals.Queries;

public static class QuerySet
{
    // every evals/queries/*.json, checked against the corpus: ids unique, every expected path present.
    // a query whose answers all sit under pruned dirs can't be found by design and is set aside, not scored
    public static (List<EvalQuery> Scored, List<EvalQuery> Pruned) Load(string directory, IReadOnlySet<string> corpus, Func<string, bool> isPruned)
    {
        var scored = new List<EvalQuery>();
        var pruned = new List<EvalQuery>();
        var ids = new HashSet<string>();
        foreach (var file in Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            foreach (var query in JsonSerializer.Deserialize(File.ReadAllText(file), EvalJson.Default.EvalQueryArray)!)
            {
                if (!ids.Add(query.Id))
                {
                    throw new InvalidOperationException($"{Path.GetFileName(file)}: duplicate id {query.Id}");
                }

                foreach (var expected in query.Expected)
                {
                    if (!corpus.Contains(expected.TrimEnd('/')))
                    {
                        throw new InvalidOperationException($"{query.Id} '{query.Query}' expects {expected}, which the corpus doesn't hold");
                    }
                }

                (query.Expected.All(isPruned) ? pruned : scored).Add(query);
            }
        }
        return (scored, pruned);
    }
}
