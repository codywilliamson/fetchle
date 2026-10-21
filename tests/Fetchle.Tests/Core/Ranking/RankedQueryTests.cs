using Fetchle.Core.Index;
using Fetchle.Core.Ranking;
using Fetchle.Core.Search;

namespace Fetchle.Tests.Core.Ranking;

public class RankedQueryTests
{
    static readonly string Root = Path.Join(Path.GetTempPath(), "fetchle-ranked-never-created");

    static readonly string[] Files =
    [
        "AppData/Roaming/Code/User/settings.json",
        "AppData/Roaming/Code/User/keybindings.json",
        "AppData/Local/GitHubDesktop/GitHubDesktop.exe",
        "AppData/Roaming/Microsoft/Windows/PowerShell/PSReadLine/ConsoleHost_history.txt",
        "source/fetchle/src/Fetchle.Core/Walking/PruneRules.cs",
        "source/fetchle/src/Fetchle.Core/Naive/NaiveWalker.cs",
        "source/other/settings/readme.md",
    ];

    static string[] Search(string query, EntryType type = EntryType.Any)
    {
        var tree = TreeFromPaths.Build(Root, Files.Select(f => (f, false)));
        using var snapshot = Snapshot.FromBytes(SnapshotWriter.ToBytes(tree, DateTimeOffset.UnixEpoch));
        var request = new SearchRequest(query, [Root], 5, TimeSpan.FromSeconds(10), Type: type);
        var outcome = RankedQuery.Run(snapshot, request, QueryTerms.Parse(query), [new IndexScope(0, Root)], Deadline.After(request.Budget, System.Diagnostics.Stopwatch.GetTimestamp()));
        return [.. RankedQuery.ToHits(snapshot, outcome.Ranked, 5).Select(h => Path.GetRelativePath(Root, h.Path).Replace('\\', '/'))];
    }

    [Test]
    public async Task All_terms_in_the_path_beat_one_term_in_the_name() =>
        await Assert.That(Search("vscode code user settings", EntryType.Files)[0]).IsEqualTo("AppData/Roaming/Code/User/settings.json");

    [Test]
    public async Task A_typo_still_finds_the_file() =>
        await Assert.That(Search("consolehost histroy")[0]).IsEqualTo("AppData/Roaming/Microsoft/Windows/PowerShell/PSReadLine/ConsoleHost_history.txt");

    [Test]
    public async Task One_word_matches_a_camel_case_compound_by_prefix() =>
        await Assert.That(Search("github")[0]).IsEqualTo("AppData/Local/GitHubDesktop");

    [Test]
    public async Task Camel_case_query_matches_the_whole_name() =>
        await Assert.That(Search("NaiveWalker")[0]).IsEqualTo("source/fetchle/src/Fetchle.Core/Naive/NaiveWalker.cs");

    [Test]
    public async Task Camel_case_query_falls_back_to_its_parts() =>
        await Assert.That(Search("PruneWalking")).Contains("source/fetchle/src/Fetchle.Core/Walking/PruneRules.cs");

    [Test]
    public async Task Type_filter_drops_dirs() =>
        await Assert.That(Search("settings", EntryType.Files)).DoesNotContain("source/other/settings");

    [Test]
    public async Task Nothing_matching_returns_nothing() =>
        await Assert.That(Search("zzzqqq")).IsEmpty();
}
