namespace Fetchle.Core.Search;

public interface IFileSearch
{
    SearchResult Search(SearchRequest request, CancellationToken cancellationToken);

    IndexStatus GetStatus(IReadOnlyList<string> roots);
}
