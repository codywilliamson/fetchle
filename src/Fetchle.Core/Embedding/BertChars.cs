using System.Globalization;
using System.Text;

namespace Fetchle.Core.Embedding;

// the character classes HF's BertNormalizer and BertPreTokenizer branch on
static class BertChars
{
    // dropped by clean_text. tab, newline and carriage return count as whitespace instead
    public static bool IsDropped(Rune rune)
    {
        var value = rune.Value;
        if (value == 0 || value == 0xFFFD)
        {
            return true;
        }

        if (value is '\t' or '\n' or '\r')
        {
            return false;
        }

        return Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format
            or UnicodeCategory.PrivateUse or UnicodeCategory.Surrogate or UnicodeCategory.OtherNotAssigned;
    }

    // handle_chinese_chars: these get a space on each side so every one is its own word
    public static bool IsCjk(Rune rune)
    {
        var v = rune.Value;
        return v is >= 0x4E00 and <= 0x9FFF
            or >= 0x3400 and <= 0x4DBF
            or >= 0x20000 and <= 0x2A6DF
            or >= 0x2A700 and <= 0x2B73F
            or >= 0x2B740 and <= 0x2B81F
            or >= 0x2B820 and <= 0x2CEAF
            or >= 0xF900 and <= 0xFAFF
            or >= 0x2F800 and <= 0x2FA1F;
    }

    // BertPreTokenizer: every ascii punctuation char (symbols like $ + < = > ^ ` | ~ included) and every unicode P* char
    public static bool IsPunctuation(Rune rune)
    {
        var v = rune.Value;
        if (v < 0x80)
        {
            return v is >= 33 and <= 47 or >= 58 and <= 64 or >= 91 and <= 96 or >= 123 and <= 126;
        }

        return Rune.GetUnicodeCategory(rune) is UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation
            or UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation or UnicodeCategory.InitialQuotePunctuation
            or UnicodeCategory.FinalQuotePunctuation or UnicodeCategory.OtherPunctuation;
    }
}
