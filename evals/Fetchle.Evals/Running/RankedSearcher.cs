using System.Diagnostics;
using Fetchle.Core.Index;
using Fetchle.Core.Ranking;
using Fetchle.Core.Ranking.Semantic;
using Fetchle.Core.Search;
using Fetchle.Evals.Corpus;

namespace Fetchle.Evals.Running;

// the v0.2 ranking, straight on the corpus snapshot
public sealed class RankedSearcher(bool lexicalOnly = false, bool semanticOnly = false) : IEvalSearcher
{
    public string Name => lexicalOnly ? "ranked, lexical only" : "ranked";

    public SearchResult Search(EvalCorpus corpus, string query, int limit)
    {
        var start = Stopwatch.GetTimestamp();
        var request = new SearchRequest(query, [corpus.Root], limit, TimeSpan.FromSeconds(30));
        using var semantic = lexicalOnly ? null : SemanticScores.Build(corpus.Snapshot, corpus.Vectors, corpus.Model, query);
        var outcome = RankedQuery.Run(corpus.Snapshot, request, QueryTerms.Parse(query), [new IndexScope(0, corpus.Root)], Deadline.After(request.Budget, start), semantic);
        if (semanticOnly)
        {
            outcome = outcome with { Lexical = [] };
        }
        return new SearchResult(Fusion.Fuse([(corpus.Snapshot, outcome)], limit), outcome.Total, Stopwatch.GetElapsedTime(start), null);
    }
}
