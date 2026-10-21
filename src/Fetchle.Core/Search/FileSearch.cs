using Fetchle.Core.Ranking.Semantic;
using Fetchle.Core.Walking;

namespace Fetchle.Core.Search;

// walks every root live with FastWalker into an in-memory tree and ranks it, no index involved
public sealed class FileSearch : IFileSearch
{
    readonly PruneRules _prune;
    readonly ITextEncoder? _model;

    public FileSearch(PruneRules prune) : this(prune, null)
    {
    }

    public FileSearch(PruneRules prune, ITextEncoder? model)
    {
        _prune = prune;
        _model = model;
    }

    public SearchResult Search(SearchRequest request, CancellationToken cancellationToken)
    {
        using var session = SearchSession.Open(null, _prune, _model);
        return session.Search(request, cancellationToken);
    }

    internal static string NormalizeRoot(string root)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw InvalidRootException.BadSyntax(root);
        }
    }

    public IndexStatus GetStatus(IReadOnlyList<string> roots) =>
        new([.. roots.Select(r => new RootStatus(Path.GetFullPath(r), FileCount: null, LastFullScan: null))], VectorsComplete: false, WatcherLive: false);
}
