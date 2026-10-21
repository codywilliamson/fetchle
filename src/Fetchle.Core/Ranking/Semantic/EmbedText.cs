using Fetchle.Core.Ranking.Tokens;

namespace Fetchle.Core.Ranking.Semantic;

// what of a name the embedder sees: its words and extension, space separated. numbers, noise and compounds stay
// out, so "Microsoft.WindowsTerminal_8wekyb3d8bbwe" embeds as "microsoft windows terminal" and "2.1.286" as nothing
static class EmbedText
{
    const int MAX_TOKENS = 64;

    // writes into text (at least 2x name's length) and returns how many chars; 0 means nothing to embed
    public static int Write(ReadOnlySpan<char> name, Span<char> lowered, Span<char> text)
    {
        Span<Token> tokens = stackalloc Token[MAX_TOKENS];
        var count = SegmentTokenizer.Tokenize(name, isFile: true, lowered, tokens);
        var length = 0;
        for (var t = 0; t < count; t++)
        {
            if (tokens[t].Kind is not (TokenKind.Word or TokenKind.Extension))
            {
                continue;
            }

            if (length > 0)
            {
                text[length++] = ' ';
            }

            tokens[t].In(lowered).CopyTo(text[length..]);
            length += tokens[t].Length;
        }
        return length;
    }

    public static string Of(string name)
    {
        var lowered = new char[name.Length];
        var text = new char[name.Length * 2 + 1];
        return new string(text, 0, Write(name, lowered, text));
    }
}
