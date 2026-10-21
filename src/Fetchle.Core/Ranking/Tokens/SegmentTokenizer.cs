namespace Fetchle.Core.Ranking.Tokens;

// splits one name segment into lowercase word tokens, the unit both retrievers work in.
// separators are anything not a letter or digit; inside a chunk, camelCase humps and letter/digit
// boundaries split again. "ConsoleHost_history.txt" -> console host history txt(ext),
// "HTMLParser2" -> html parser 2, "Microsoft.WindowsTerminal_8wekyb3d8bbwe" -> microsoft windows terminal.
// noise chunks (see NoiseFilter) stay whole as one Noise token, before the digit split can shred them into fake words
public static class SegmentTokenizer
{
    // more chunks than this in one name is noise of its own; the rest are ignored
    public const int MAX_CHUNKS = 64;
    const int MAX_EXTENSION = 5;
    const int GUID_CHUNKS = 5;

    // lowered must be at least segment.Length long. returns the number of tokens written; stops when tokens is full
    public static int Tokenize(ReadOnlySpan<char> segment, bool isFile, Span<char> lowered, Span<Token> tokens)
    {
        for (var i = 0; i < segment.Length; i++)
        {
            lowered[i] = char.ToLowerInvariant(segment[i]);
        }

        Span<(int Start, int Length)> chunks = stackalloc (int, int)[MAX_CHUNKS];
        var chunkCount = Chunk(segment, chunks);
        chunks = chunks[..chunkCount];

        var count = 0;
        for (var c = 0; c < chunks.Length && count < tokens.Length; c++)
        {
            var (start, length) = chunks[c];
            if (NoiseFilter.IsGuidAt(segment, chunks, c))
            {
                // the whole guid, dashes included, is one token
                var (lastStart, lastLength) = chunks[c + GUID_CHUNKS - 1];
                tokens[count++] = new Token(start, lastStart + lastLength - start, TokenKind.Noise);
                c += GUID_CHUNKS - 1;
                continue;
            }

            var chunk = segment.Slice(start, length);
            if (NoiseFilter.IsNoise(chunk) || NoiseFilter.IsPublisherId(segment, start, length))
            {
                tokens[count++] = new Token(start, length, TokenKind.Noise);
                continue;
            }

            if (isFile && c == chunks.Length - 1 && c > 0 && IsExtension(segment, start, length))
            {
                tokens[count++] = new Token(start, length, TokenKind.Extension);
                continue;
            }

            var before = count;
            count = SplitChunk(chunk, start, tokens, count);
            if (count - before > 1 && count < tokens.Length)
            {
                tokens[count++] = new Token(start, length, TokenKind.Compound);
            }
        }
        return count;
    }

    static int Chunk(ReadOnlySpan<char> segment, Span<(int Start, int Length)> chunks)
    {
        var count = 0;
        var i = 0;
        while (i < segment.Length && count < chunks.Length)
        {
            while (i < segment.Length && !char.IsLetterOrDigit(segment[i]))
            {
                i++;
            }

            var start = i;
            while (i < segment.Length && char.IsLetterOrDigit(segment[i]))
            {
                i++;
            }

            if (i > start)
            {
                chunks[count++] = (start, i - start);
            }
        }
        return count;
    }

    // the final chunk right after the last dot, short and with a letter: "json", "mp4", not "286" in "2.1.286"
    static bool IsExtension(ReadOnlySpan<char> segment, int start, int length)
    {
        if (length > MAX_EXTENSION || segment[start - 1] != '.' || start + length != segment.Length)
        {
            return false;
        }

        foreach (var c in segment.Slice(start, length))
        {
            if (char.IsLetter(c))
            {
                return true;
            }
        }
        return false;
    }

    // camelCase humps and letter/digit boundaries. an uppercase run hands its last letter to a following lowercase
    // run, so "HTMLParser" splits as html parser
    static int SplitChunk(ReadOnlySpan<char> chunk, int offset, Span<Token> tokens, int count)
    {
        var start = 0;
        for (var i = 1; i <= chunk.Length && count < tokens.Length; i++)
        {
            if (i < chunk.Length && !IsBoundary(chunk, i))
            {
                continue;
            }

            var kind = char.IsDigit(chunk[start]) ? TokenKind.Number : TokenKind.Word;
            tokens[count++] = new Token(offset + start, i - start, kind);
            start = i;
        }
        return count;
    }

    static bool IsBoundary(ReadOnlySpan<char> chunk, int i)
    {
        var previous = chunk[i - 1];
        var c = chunk[i];
        if (char.IsDigit(previous) != char.IsDigit(c))
        {
            return true;
        }

        if (char.IsLower(previous) && char.IsUpper(c))
        {
            return true;
        }

        // "HTMLParser": the P starts a new word when an uppercase run meets a lowercase letter
        return char.IsUpper(previous) && char.IsUpper(c) && i + 1 < chunk.Length && char.IsLower(chunk[i + 1]);
    }
}
