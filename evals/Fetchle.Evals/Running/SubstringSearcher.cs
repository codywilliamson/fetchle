using System.Diagnostics;
using Fetchle.Core.Index;
using Fetchle.Core.Naive;
using Fetchle.Core.Search;
using Fetchle.Evals.Corpus;

namespace Fetchle.Evals.Running;

// v0.1's ranking: the query as one substring of a name or path. kept as the "before" number
public sealed class SubstringSearcher : IEvalSearcher
{
    public string Name => "substring (v0.1)";

    public SearchResult Search(EvalCorpus corpus, string query, int limit)
    {
        var start = Stopwatch.GetTimestamp();
        var request = new SearchRequest(query, [corpus.Root], limit, TimeSpan.FromSeconds(30));
        var hits = new TopHits(limit);
        var total = 0;
        IndexQuery.Run(corpus.Snapshot, request, [new IndexScope(0, corpus.Root)], hits, ref total, Deadline.After(request.Budget, start));
        return new SearchResult(hits.ToSortedList(), total, Stopwatch.GetElapsedTime(start), null);
    }
}
