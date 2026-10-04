using Fetchle.Core.Naive;

namespace Fetchle.Tests.Core;

public class SubstringRankerTests
{
    [Test]
    public async Task Name_match_beats_path_match()
    {
        var ranker = new SubstringRanker("settings");
        await Assert.That(ranker.Score("app/Settings.json", "Settings.json")).IsEqualTo(SubstringRanker.NAME_MATCH);
        await Assert.That(ranker.Score("settings/app.json", "app.json")).IsEqualTo(SubstringRanker.PATH_MATCH);
        await Assert.That(ranker.Score("app/main.cs", "main.cs")).IsEqualTo(0d);
    }

    [Test]
    public async Task Matches_across_separators() =>
        await Assert.That(new SubstringRanker("user/settings").Score("Code/User/settings.json", "settings.json")).IsEqualTo(SubstringRanker.PATH_MATCH);
}
