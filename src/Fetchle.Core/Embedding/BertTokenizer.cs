using System.Text;

namespace Fetchle.Core.Embedding;

// model2vec's tokenizer, streamed: BertNormalizer (clean_text, handle_chinese_chars, strip accents, lowercase),
// BertPreTokenizer (split on whitespace, each punctuation char its own word), then WordPiece per word. no
// [CLS]/[SEP], and [UNK] never reaches the sink. one pass over the text, only stack buffers
//
// not mirrored: HF also pulls literal special-token text such as "[CLS]" out of the input before normalizing,
// and lowercases with full Unicode rules where this uses the per-rune invariant mapping
static class BertTokenizer
{
    public static void Tokenize<TSink>(StaticModel model, ReadOnlySpan<char> text, ref TSink sink)
        where TSink : ITokenSink, allows ref struct
    {
        var maxRunes = model.MaxWordChars;
        Span<char> word = stackalloc char[maxRunes * 2];
        var used = 0;
        var runes = 0;
        var overflow = false;

        var i = 0;
        while (i < text.Length)
        {
            Rune.DecodeFromUtf16(text[i..], out var rune, out var consumed);
            i += consumed;
            if (BertChars.IsDropped(rune))
            {
                continue;
            }

            if (Rune.IsWhiteSpace(rune))
            {
                Flush(model, word, ref used, ref runes, ref overflow, ref sink);
                continue;
            }

            if (BertChars.IsCjk(rune))
            {
                Flush(model, word, ref used, ref runes, ref overflow, ref sink);
                Append(rune, word, maxRunes, ref used, ref runes, ref overflow);
                Flush(model, word, ref used, ref runes, ref overflow, ref sink);
                continue;
            }

            if (Hangul.TryDecompose(rune, out var lead, out var vowel, out var trail))
            {
                Append(lead, word, maxRunes, ref used, ref runes, ref overflow);
                Append(vowel, word, maxRunes, ref used, ref runes, ref overflow);
                if (trail.Value != 0)
                {
                    Append(trail, word, maxRunes, ref used, ref runes, ref overflow);
                }

                continue;
            }

            var stripped = Accents.Strip(rune);
            if (stripped is null)
            {
                continue;
            }

            var lowered = Rune.ToLowerInvariant(stripped.Value);
            if (BertChars.IsPunctuation(lowered))
            {
                Flush(model, word, ref used, ref runes, ref overflow, ref sink);
                Append(lowered, word, maxRunes, ref used, ref runes, ref overflow);
                Flush(model, word, ref used, ref runes, ref overflow, ref sink);
                continue;
            }

            Append(lowered, word, maxRunes, ref used, ref runes, ref overflow);
        }

        Flush(model, word, ref used, ref runes, ref overflow, ref sink);
    }

    static void Append(Rune rune, Span<char> word, int maxRunes, ref int used, ref int runes, ref bool overflow)
    {
        if (overflow)
        {
            return;
        }

        if (runes == maxRunes)
        {
            overflow = true;
            return;
        }

        used += rune.EncodeToUtf16(word[used..]);
        runes++;
    }

    // a word past max_input_chars_per_word is [UNK] as a whole
    static void Flush<TSink>(StaticModel model, scoped ReadOnlySpan<char> word, ref int used, ref int runes, ref bool overflow, ref TSink sink)
        where TSink : ITokenSink, allows ref struct
    {
        if (used > 0 && !overflow)
        {
            WordPiece.Encode(model, word[..used], ref sink);
        }

        used = 0;
        runes = 0;
        overflow = false;
    }
}
