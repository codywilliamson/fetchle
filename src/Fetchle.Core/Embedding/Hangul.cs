using System.Text;

namespace Fetchle.Core.Embedding;

// NFD splits a precomposed hangul syllable into conjoining jamo (Unicode 3.12), and the vocab holds jamo, not syllables
static class Hangul
{
    const int SYLLABLE_BASE = 0xAC00;
    const int SYLLABLE_COUNT = 11172;
    const int LEAD_BASE = 0x1100;
    const int VOWEL_BASE = 0x1161;
    const int TRAIL_BASE = 0x11A7;
    const int TRAIL_COUNT = 28;
    const int VOWEL_COUNT = 21;

    // lead and vowel always, trail is 0 when the syllable has none
    public static bool TryDecompose(Rune rune, out Rune lead, out Rune vowel, out Rune trail)
    {
        var index = rune.Value - SYLLABLE_BASE;
        if ((uint)index >= SYLLABLE_COUNT)
        {
            lead = vowel = trail = default;
            return false;
        }

        lead = new Rune(LEAD_BASE + index / (VOWEL_COUNT * TRAIL_COUNT));
        vowel = new Rune(VOWEL_BASE + index % (VOWEL_COUNT * TRAIL_COUNT) / TRAIL_COUNT);
        var t = index % TRAIL_COUNT;
        trail = t == 0 ? default : new Rune(TRAIL_BASE + t);
        return true;
    }
}
