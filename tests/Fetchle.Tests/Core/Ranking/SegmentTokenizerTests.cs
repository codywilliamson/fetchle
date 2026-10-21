using Fetchle.Core.Ranking.Tokens;

namespace Fetchle.Tests.Core.Ranking;

public class SegmentTokenizerTests
{
    static string Split(string segment, bool isFile = true)
    {
        var lowered = new char[segment.Length];
        var tokens = new Token[32];
        var count = SegmentTokenizer.Tokenize(segment, isFile, lowered, tokens);
        return string.Join(' ', tokens[..count].Select(t => Label(t, lowered)));
    }

    static string Label(Token t, char[] lowered) => t.Kind switch
    {
        TokenKind.Extension => $"{new string(t.In(lowered))}(ext)",
        TokenKind.Compound => $"[{new string(t.In(lowered))}]",
        TokenKind.Noise => $"{{{new string(t.In(lowered))}}}",
        _ => new string(t.In(lowered)),
    };

    [Test]
    [Arguments("ConsoleHost_history.txt", "console host [consolehost] history txt(ext)")]
    [Arguments("HTMLParser2.cs", "html parser 2 [htmlparser2] cs(ext)")]
    [Arguments("claude_desktop_config.json", "claude desktop config json(ext)")]
    [Arguments("GitHubDesktop.exe", "git hub desktop [githubdesktop] exe(ext)")]
    [Arguments("global-messages-db.sqlite", "global messages db sqlite")]
    [Arguments("NaiveWalker.cs", "naive walker [naivewalker] cs(ext)")]
    [Arguments("x86_64", "x 86 [x86] 64")]
    [Arguments("i18n.ts", "i 18 n [i18n] ts(ext)")]
    public async Task Splits_camel_case_separators_and_digits(string segment, string expected) =>
        await Assert.That(Split(segment)).IsEqualTo(expected);

    [Test]
    [Arguments("Microsoft.WindowsTerminal_8wekyb3d8bbwe", "microsoft windows terminal [windowsterminal] {8wekyb3d8bbwe}")]
    [Arguments("SpotifyAB.SpotifyMusic_zpdnekdrzrea0", "spotify ab [spotifyab] spotify music [spotifymusic] {zpdnekdrzrea0}")]
    [Arguments("635c1867224a", "{635c1867224a}")]
    [Arguments("k2x9qv1a.default-release", "{k2x9qv1a} default release")]
    [Arguments("{3F2504E0-4F89-11D3-9A0C-0305E82C3301}", "{3f2504e0-4f89-11d3-9a0c-0305e82c3301}")]
    [Arguments("cache-3f4bed3e44ad9c01", "cache {3f4bed3e44ad9c01}")]
    [Arguments("backup-20261021", "backup {20261021}")]
    [Arguments("app-3.4.9", "app 3 4 9")]
    public async Task Keeps_noise_whole_before_splitting_digits(string segment, string expected) =>
        await Assert.That(Split(segment, isFile: false)).IsEqualTo(expected);

    [Test]
    public async Task A_dotted_dir_has_no_extension() =>
        await Assert.That(Split("Fetchle.Core", isFile: false)).IsEqualTo("fetchle core");

    [Test]
    public async Task A_version_tail_is_not_an_extension() =>
        await Assert.That(Split("claude-2.1.286")).IsEqualTo("claude 2 1 286");

    [Test]
    public async Task Words_that_look_hexish_survive() =>
        await Assert.That(Split("decade.md")).IsEqualTo("decade md(ext)");

    [Test]
    public async Task Stops_when_the_token_buffer_is_full()
    {
        var lowered = new char[32];
        var tokens = new Token[2];
        await Assert.That(SegmentTokenizer.Tokenize("one two three four", false, lowered, tokens)).IsEqualTo(2);
    }
}
