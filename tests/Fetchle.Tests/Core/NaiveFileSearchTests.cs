using Fetchle.Core.Naive;
using Fetchle.Core.Search;
using Fetchle.Core.Walking;

namespace Fetchle.Tests.Core;

public class NaiveFileSearchTests
{
    [Test]
    public async Task Bad_root_syntax_is_an_invalid_root_not_a_crash()
    {
        var search = new NaiveFileSearch(PruneRules.Default);
        var request = new SearchRequest("x", ["bad\0root"], SearchRequest.DEFAULT_LIMIT, SearchRequest.DefaultBudget);

        var e = await Assert.That(() => search.Search(request, CancellationToken.None)).Throws<InvalidRootException>();
        await Assert.That(e!.Message).StartsWith("invalid root path");
    }
}
