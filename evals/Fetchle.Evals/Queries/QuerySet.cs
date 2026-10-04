using System.Text.Json;
using Fetchle.Evals.Reporting;

namespace Fetchle.Evals.Queries;

public static class QuerySet
{
    public static EvalQuery[] Load(string path, string corpus)
    {
        var queries = JsonSerializer.Deserialize(File.ReadAllText(path), EvalJson.Default.EvalQueryArray)!;
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
        return queries;
    }
}
