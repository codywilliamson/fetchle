namespace Fetchle.Core.Search;

// the one seam the cli and mcp server depend on. NaiveFileSearch is the placeholder;
// the indexed search (walker -> path store -> lexical + semantic -> rrf) replaces it
public interface IFileSearch
{
    // throws InvalidRootException before searching if any root is missing or not a valid path.
    // stops promptly and throws OperationCanceledException when cancellationToken fires; mcp passes
    // the client's request token here. (the naive placeholder only checks it between roots)
    SearchResult Search(SearchRequest request, CancellationToken cancellationToken);

    IndexStatus GetStatus(IReadOnlyList<string> roots);
}
