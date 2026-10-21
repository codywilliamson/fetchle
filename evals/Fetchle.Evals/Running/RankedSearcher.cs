using System.Diagnostics;
using Fetchle.Core.Index;
using Fetchle.Core.Ranking;
using Fetchle.Core.Search;
using Fetchle.Evals.Corpus;

namespace Fetchle.Evals.Running;

// the v0.2 ranking, straight on the corpus snapshot
public sealed class RankedSearcher : IEvalSearcher
{
    public string Name => "ranked";

    public SearchResult Search(EvalCorpus corpus, string query, int limit)
    {
        var start = Stopwatch.GetTimestamp();
        var request = new SearchRequest(query, [corpus.Root], limit, TimeSpan.FromSeconds(30));
        var outcome = RankedQuery.Run(corpus.Snapshot, request, QueryTerms.Parse(query), [new IndexScope(0, corpus.Root)], Deadline.After(request.Budget, start));
        return new SearchResult(Fusion.Fuse([(corpus.Snapshot, outcome)], limit), outcome.Total, Stopwatch.GetElapsedTime(start), null);
    }
}
