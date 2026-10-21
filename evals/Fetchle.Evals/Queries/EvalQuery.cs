namespace Fetchle.Evals.Queries;

// one judged query. ids start with the source: s- spike, m- mined from transcripts, b- written blind from the corpus
public sealed record EvalQuery(string Id, string Query, string[] Expected, string Kind, string? Note)
{
    public string Source => Id[..Id.IndexOf('-')];
}
