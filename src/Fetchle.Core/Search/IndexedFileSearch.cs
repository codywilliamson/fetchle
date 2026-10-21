using Fetchle.Core.Index;
using Fetchle.Core.Ranking.Semantic;
using Fetchle.Core.Walking;

namespace Fetchle.Core.Search;

// answers from the snapshot for every root it covers and walks the rest live, one SearchSession per call.
// a snapshot that exists but fails validation throws IndexUnreadableException rather than guessing
public sealed class IndexedFileSearch : IFileSearch
{
    readonly PruneRules _prune;
    readonly IndexStore _store;
    readonly ITextEncoder? _model;

    public IndexedFileSearch(PruneRules prune, IndexStore store) : this(prune, store, null)
    {
    }

    public IndexedFileSearch(PruneRules prune, IndexStore store, ITextEncoder? model)
    {
        _prune = prune;
        _store = store;
        _model = model;
    }

    public SearchResult Search(SearchRequest request, CancellationToken cancellationToken)
    {
        using var session = SearchSession.Open(_store, _prune, _model);
        return session.Search(request, cancellationToken);
    }

    public IndexStatus GetStatus(IReadOnlyList<string> roots)
    {
        using var session = SearchSession.Open(_store, _prune, _model);
        return session.GetStatus(roots);
    }
}
