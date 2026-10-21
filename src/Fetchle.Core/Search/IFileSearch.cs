namespace Fetchle.Core.Search;

public interface IFileSearch
{
    SearchResult Search(SearchRequest request, CancellationToken cancellationToken);

    IndexStatus GetStatus(IReadOnlyList<string> roots);

    // true while an index rebuild holds the write lock, so answers may lag the disk
    bool IsRebuilding();
}
