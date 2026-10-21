using System.Text.Json;
using Fetchle.Core.Search;
using Fetchle.Mcp;
using ModelContextProtocol.Protocol;

namespace Fetchle.Tests.Core;

// find_files and index_status footers say when a rebuild holds the write lock, so an agent doesn't trust a miss
public class RebuildNoticeTests
{
    static Tools ToolsOver(bool rebuilding) => new(new FakeSearch(rebuilding), () => [Path.GetTempPath()]);

    static string TextOf(CallToolResult result) => ((TextContentBlock)result.Content[0]).Text;

    static Dictionary<string, JsonElement> Query() => new() { ["query"] = JsonSerializer.SerializeToElement("settings") };

    [Test]
    public async Task Find_files_footer_names_a_running_rebuild()
    {
        var text = TextOf(ToolsOver(rebuilding: true).Call("find_files", Query(), CancellationToken.None));

        await Assert.That(text).EndsWith(Tools.REBUILDING_NOTICE);
    }

    [Test]
    public async Task Index_status_names_a_running_rebuild()
    {
        var text = TextOf(ToolsOver(rebuilding: true).Call("index_status", null, CancellationToken.None));

        await Assert.That(text).EndsWith(Tools.REBUILDING_NOTICE);
    }

    [Test]
    public async Task Footers_stay_silent_when_no_rebuild_runs()
    {
        var find = TextOf(ToolsOver(rebuilding: false).Call("find_files", Query(), CancellationToken.None));
        var status = TextOf(ToolsOver(rebuilding: false).Call("index_status", null, CancellationToken.None));

        await Assert.That(find).DoesNotContain("rebuild");
        await Assert.That(status).DoesNotContain("rebuild");
    }

    sealed class FakeSearch(bool rebuilding) : IFileSearch
    {
        public SearchResult Search(SearchRequest request, CancellationToken cancellationToken) => new([], 0, TimeSpan.Zero, null);

        public IndexStatus GetStatus(IReadOnlyList<string> roots) => new([], VectorsComplete: false, WatcherLive: false);

        public bool IsRebuilding() => rebuilding;
    }
}
