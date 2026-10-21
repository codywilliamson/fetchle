using System.Globalization;
using System.Text;

namespace Fetchle.Core.Embedding;

// strips accents the way NFD plus "drop non-spacing marks" does, without string.Normalize. under
// InvariantGlobalization Normalize silently returns its input on windows (checked: "Ünï" stays 3 chars), so
// it can't be relied on. the table covers latin-1 through latin extended-b, latin extended additional (vietnamese),
// greek and cyrillic. hangul is split into jamo in Hangul.cs. NOT covered: the Kelvin/Angstrom/Ohm singleton
// signs and accented letters in scripts outside those blocks (AccentTests pins this).
static partial class Accents
{
    // the stripped rune, or null when the rune is a combining mark that disappears entirely
    public static Rune? Strip(Rune rune)
    {
        if (rune.Value < 0xC0)
        {
            return rune;
        }

        if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.NonSpacingMark)
        {
            return null;
        }

        if (rune.IsBmp)
        {
            return new Rune(Table((char)rune.Value));
        }

        return rune;
    }
}
