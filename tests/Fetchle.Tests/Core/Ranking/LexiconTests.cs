using System.Text;
using Fetchle.Core.Index;
using Fetchle.Core.Ranking.Lexical;

namespace Fetchle.Tests.Core.Ranking;

public class LexiconTests
{
    static readonly string[] Names = ["settings.json", "SettingsPanel.tsx", "ConsoleHost_history.txt", "GitHubDesktop.exe", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "app-3.4.9"];

    static LexiconParts Parts()
    {
        var segments = new SegmentTable();
        foreach (var name in Names)
        {
            segments.Intern(name);
        }
        return LexiconBuilder.Build(segments, [true, true, true, true, false, false]);
    }

    static Lexicon Over(LexiconParts p) => new(p.TokenOffsets, p.TokenBlob, p.PostingOffsets, p.Postings, p.FuzzyKeys, p.FuzzyTokens, p.SegmentWordCounts);

    static uint[] SegmentsOf(LexiconParts parts, string token)
    {
        var lexicon = Over(parts);
        var rank = lexicon.Find(Encoding.UTF8.GetBytes(token));
        return rank < 0 ? [] : [.. lexicon.Postings(rank).ToArray().Select(Posting.Segment)];
    }

    static string[] AllTokens(LexiconParts parts) =>
        [.. Enumerable.Range(0, Over(parts).TokenCount).Select(r => Encoding.UTF8.GetString(Over(parts).Token(r)))];

    static uint KindOf(LexiconParts parts, string token)
    {
        var lexicon = Over(parts);
        return Posting.Kind(lexicon.Postings(lexicon.Find(Encoding.UTF8.GetBytes(token)))[0]);
    }

    static string[] WithPrefix(LexiconParts parts, string prefix)
    {
        var (start, end) = Over(parts).PrefixRange(Encoding.UTF8.GetBytes(prefix));
        return [.. Enumerable.Range(start, end - start).Select(r => Encoding.UTF8.GetString(Over(parts).Token(r)))];
    }

    static HashSet<string> FuzzyCandidates(LexiconParts parts, string term)
    {
        Span<uint> keys = stackalloc uint[FuzzyKeys.MAX_LENGTH + 1];
        var count = FuzzyKeys.Keys(term, keys);
        var candidates = new HashSet<string>();
        for (var k = 0; k < count; k++)
        {
            foreach (var rank in Over(parts).FuzzyTokens(keys[k]))
            {
                candidates.Add(Encoding.UTF8.GetString(Over(parts).Token((int)rank)));
            }
        }
        return candidates;
    }

    [Test]
    public async Task Tokens_come_out_sorted_by_utf8_bytes()
    {
        var tokens = AllTokens(Parts());
        await Assert.That(tokens).IsEquivalentTo(tokens.Order(StringComparer.Ordinal).ToArray());
        await Assert.That(tokens.Distinct().Count()).IsEqualTo(tokens.Length);
    }

    [Test]
    public async Task Postings_point_back_at_every_segment_holding_the_token()
    {
        var parts = Parts();
        await Assert.That(SegmentsOf(parts, "settings")).IsEquivalentTo(new uint[] { 0, 1 });
        await Assert.That(SegmentsOf(parts, "githubdesktop")).IsEquivalentTo(new uint[] { 3 });
        await Assert.That(SegmentsOf(parts, "terminal")).IsEquivalentTo(new uint[] { 4 });
        await Assert.That(SegmentsOf(parts, "8wekyb3d8bbwe")).IsEquivalentTo(new uint[] { 4 });
        await Assert.That(SegmentsOf(parts, "nope")).IsEmpty();
    }

    [Test]
    public async Task Postings_carry_how_the_token_appeared()
    {
        var parts = Parts();
        await Assert.That(KindOf(parts, "json")).IsEqualTo(Posting.EXTENSION);
        await Assert.That(KindOf(parts, "8wekyb3d8bbwe")).IsEqualTo(Posting.NOISE);
        await Assert.That(KindOf(parts, "3")).IsEqualTo(Posting.NUMBER);
    }

    [Test]
    public async Task Prefix_range_covers_exactly_the_tokens_that_start_with_it()
    {
        var parts = Parts();
        await Assert.That(WithPrefix(parts, "git")).IsEquivalentTo(new[] { "git", "githubdesktop" });
        await Assert.That(WithPrefix(parts, "zzz")).IsEmpty();
    }

    [Test]
    [Arguments("setings")]
    [Arguments("settinsg")]
    [Arguments("settingz")]
    [Arguments("settingss")]
    public async Task One_edit_finds_the_token_through_a_shared_key(string typo) =>
        await Assert.That(FuzzyCandidates(Parts(), typo)).Contains("settings");

    [Test]
    public async Task Numbers_and_noise_get_no_fuzzy_keys()
    {
        var parts = Parts();
        var noise = (uint)Array.IndexOf(AllTokens(parts), "8wekyb3d8bbwe");
        var number = (uint)Array.IndexOf(AllTokens(parts), "3");
        await Assert.That(parts.FuzzyTokens).DoesNotContain(noise);
        await Assert.That(parts.FuzzyTokens).DoesNotContain(number);
    }

    [Test]
    [Arguments("settings", "setings", true)]
    [Arguments("settings", "settigns", true)]
    [Arguments("settings", "settingz", true)]
    [Arguments("settings", "settingsx", true)]
    [Arguments("settings", "setting", true)]
    [Arguments("settings", "sett", false)]
    [Arguments("settings", "sxttingz", false)]
    public async Task Within_one_is_restricted_damerau(string a, string b, bool expected) =>
        await Assert.That(FuzzyKeys.WithinOne(a, b)).IsEqualTo(expected);

    [Test]
    public async Task Word_counts_skip_compounds_and_noise()
    {
        var parts = Parts();
        // settings json, settings panel tsx, console host history txt, git hub desktop exe, microsoft windows terminal, app 3 4 9
        await Assert.That(parts.SegmentWordCounts).IsEquivalentTo(new byte[] { 2, 3, 4, 4, 3, 4 });
    }
}
