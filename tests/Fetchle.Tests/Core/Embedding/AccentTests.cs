using System.Text;
using Fetchle.Core.Embedding;

namespace Fetchle.Tests.Core.Embedding;

// why Accents exists and where it stops
public class AccentTests
{
    [Test]
    public async Task Strip_matches_nfd_without_marks_over_every_covered_char()
    {
        // string.Normalize is a no-op under InvariantGlobalization on some platforms, in which case there's nothing to compare to
        Skip.Unless("é".Normalize(NormalizationForm.FormD).Length == 2, "string.Normalize doesn't decompose in this build");

        var mismatches = new List<string>();
        foreach (var (start, end) in (ReadOnlySpan<(int, int)>)[(0xC0, 0x24F), (0x370, 0x4FF), (0x1E00, 0x1EFF)])
        {
            for (var cp = start; cp <= end; cp++)
            {
                var rune = new Rune(cp);
                var expected = new string(rune.ToString().Normalize(NormalizationForm.FormD)
                    .Where(c => char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
                var actual = Accents.Strip(rune)?.ToString() ?? "";
                if (actual != expected && expected.Length == 1)
                {
                    mismatches.Add($"U+{cp:X4} {actual} != {expected}");
                }
            }
        }

        await Assert.That(mismatches).IsEmpty();
    }

    [Test]
    [Arguments("é", "e")]
    [Arguments("Ü", "U")]
    [Arguments("ñ", "n")]
    [Arguments("Å", "A")]
    [Arguments("ç", "c")]
    [Arguments("ệ", "e")]
    [Arguments("ǎ", "a")]
    [Arguments("й", "и")]
    [Arguments("ά", "α")]
    [Arguments("e", "e")]
    public async Task Common_accents_lose_them(string accented, string plain)
    {
        await Assert.That(Accents.Strip(new Rune(accented[0]))?.ToString()).IsEqualTo(plain);
    }

    [Test]
    public async Task Combining_marks_disappear()
    {
        await Assert.That(Accents.Strip(new Rune(0x0301))).IsNull();
        await Assert.That(Accents.Strip(new Rune(0x0323))).IsNull();
    }

    // the documented gap: signs that NFD maps to a letter but sit outside the generated blocks
    [Test]
    public async Task Singleton_signs_outside_the_blocks_are_left_alone()
    {
        await Assert.That(Accents.Strip(new Rune(0x212B))?.ToString()).IsEqualTo("Å");
    }

    [Test]
    public async Task Hangul_syllables_split_into_jamo()
    {
        Rune.TryCreate(0x1112, out var lead);
        await Assert.That(Hangul.TryDecompose(new Rune('한'), out var l, out var v, out var t)).IsTrue();
        await Assert.That(l).IsEqualTo(lead);
        await Assert.That(v.Value).IsEqualTo(0x1161);
        await Assert.That(t.Value).IsEqualTo(0x11AB);
        await Assert.That(Hangul.TryDecompose(new Rune('가'), out _, out _, out var none)).IsTrue();
        await Assert.That(none.Value).IsEqualTo(0);
        await Assert.That(Hangul.TryDecompose(new Rune('a'), out _, out _, out _)).IsFalse();
    }
}
