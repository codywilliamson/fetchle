using System.Buffers;
using System.Text;
using Fetchle.Core.Index;
using Fetchle.Core.Ranking.Tokens;

namespace Fetchle.Core.Ranking.Lexical;

// the lexicon as plain arrays, ready to lay into a snapshot. see Lexicon for what each one holds
sealed record LexiconParts(
    uint[] TokenOffsets,
    byte[] TokenBlob,
    uint[] PostingOffsets,
    uint[] Postings,
    uint[] FuzzyKeys,
    uint[] FuzzyTokens,
    byte[] SegmentWordCounts);

// tokenizes every unique segment once and inverts it: token -> the segments holding it. runs at rebuild time,
// per unique segment, never per entry
static class LexiconBuilder
{
    const int MAX_TOKENS_PER_SEGMENT = 64;
    const int INITIAL_POSTINGS = 1 << 16;

    public static LexiconParts Build(SegmentTable segments, ReadOnlySpan<bool> segmentIsFile)
    {
        var tokens = new SegmentTable();
        // per token, which kinds it appeared as. fuzzy keys only go to tokens that were ever a word
        var kinds = new byte[1024];
        var postingTokens = new int[INITIAL_POSTINGS];
        var postingValues = new uint[INITIAL_POSTINGS];
        var postingCount = 0;
        var wordCounts = new byte[segments.Count];
        var lowered = ArrayPool<char>.Shared.Rent(256);
        Span<Token> found = stackalloc Token[MAX_TOKENS_PER_SEGMENT];
        Span<int> ids = stackalloc int[MAX_TOKENS_PER_SEGMENT];
        try
        {
            for (var s = 0; s < segments.Count; s++)
            {
                var name = segments[s];
                if (lowered.Length < name.Length)
                {
                    ArrayPool<char>.Shared.Return(lowered);
                    lowered = ArrayPool<char>.Shared.Rent(name.Length);
                }

                var count = SegmentTokenizer.Tokenize(name, segmentIsFile[s], lowered, found);
                var words = 0;
                var unique = 0;
                for (var t = 0; t < count; t++)
                {
                    var token = found[t];
                    if (token.Kind is TokenKind.Word or TokenKind.Number or TokenKind.Extension)
                    {
                        words++;
                    }

                    var id = tokens.Intern(token.In(lowered));
                    if (id == kinds.Length)
                    {
                        Array.Resize(ref kinds, kinds.Length * 2);
                    }
                    kinds[id] |= (byte)(1 << (int)token.Kind);
                    // "SpotifyAB.SpotifyMusic" holds spotify twice; one posting per segment is enough
                    if (ids[..unique].Contains(id))
                    {
                        continue;
                    }

                    ids[unique++] = id;
                    if (postingCount == postingTokens.Length)
                    {
                        Array.Resize(ref postingTokens, postingCount * 2);
                        Array.Resize(ref postingValues, postingCount * 2);
                    }

                    postingTokens[postingCount] = id;
                    postingValues[postingCount] = Posting.Pack((uint)s, token.Kind);
                    postingCount++;
                }
                wordCounts[s] = (byte)Math.Min(words, byte.MaxValue);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(lowered);
        }

        var (rankOf, tokenOffsets, tokenBlob) = SortTokens(tokens);
        var (postingOffsets, postings) = Invert(rankOf, tokens.Count, postingTokens.AsSpan(0, postingCount), postingValues.AsSpan(0, postingCount));
        var (fuzzyKeys, fuzzyTokens) = BuildFuzzy(tokens, kinds, rankOf);
        return new LexiconParts(tokenOffsets, tokenBlob, postingOffsets, postings, fuzzyKeys, fuzzyTokens, wordCounts);
    }

    // ranks tokens by their utf-8 bytes, the order a query's prefix search compares in
    static (int[] RankOf, uint[] Offsets, byte[] Blob) SortTokens(SegmentTable tokens)
    {
        var utf8Starts = new int[tokens.Count + 1];
        var size = 0;
        for (var t = 0; t < tokens.Count; t++)
        {
            utf8Starts[t] = size;
            size += Encoding.UTF8.GetByteCount(tokens[t]);
        }
        utf8Starts[tokens.Count] = size;

        var utf8 = new byte[size];
        for (var t = 0; t < tokens.Count; t++)
        {
            Encoding.UTF8.GetBytes(tokens[t], utf8.AsSpan(utf8Starts[t]));
        }

        var order = new int[tokens.Count];
        for (var t = 0; t < order.Length; t++)
        {
            order[t] = t;
        }
        Array.Sort(order, (a, b) => utf8.AsSpan(utf8Starts[a], utf8Starts[a + 1] - utf8Starts[a])
            .SequenceCompareTo(utf8.AsSpan(utf8Starts[b], utf8Starts[b + 1] - utf8Starts[b])));

        var rankOf = new int[tokens.Count];
        var offsets = new uint[tokens.Count + 1];
        var blob = new byte[size];
        var at = 0;
        for (var rank = 0; rank < order.Length; rank++)
        {
            var t = order[rank];
            rankOf[t] = rank;
            offsets[rank] = (uint)at;
            var bytes = utf8.AsSpan(utf8Starts[t], utf8Starts[t + 1] - utf8Starts[t]);
            bytes.CopyTo(blob.AsSpan(at));
            at += bytes.Length;
        }
        offsets[order.Length] = (uint)at;
        return (rankOf, offsets, blob);
    }

    // counting sort by token rank. segments were visited in id order, so each token's list comes out ascending
    static (uint[] Offsets, uint[] Postings) Invert(int[] rankOf, int tokenCount, ReadOnlySpan<int> postingTokens, ReadOnlySpan<uint> postingValues)
    {
        var offsets = new uint[tokenCount + 1];
        foreach (var token in postingTokens)
        {
            offsets[rankOf[token] + 1]++;
        }

        for (var r = 0; r < tokenCount; r++)
        {
            offsets[r + 1] += offsets[r];
        }

        var next = offsets[..^1];
        var postings = new uint[postingTokens.Length];
        for (var p = 0; p < postingTokens.Length; p++)
        {
            postings[next[rankOf[postingTokens[p]]]++] = postingValues[p];
        }
        return (offsets, postings);
    }

    // (key, token rank) pairs sorted by key, for tokens whose length fuzzy matching covers. numbers and noise
    // are left out: "2024" one edit from "2025" is a different file, not a typo
    static (uint[] Keys, uint[] Tokens) BuildFuzzy(SegmentTable tokens, byte[] kinds, int[] rankOf)
    {
        const byte WORDISH = (1 << (int)TokenKind.Word) | (1 << (int)TokenKind.Compound) | (1 << (int)TokenKind.Extension);
        var pairs = new List<ulong>();
        Span<uint> keys = stackalloc uint[FuzzyKeys.MAX_LENGTH + 1];
        for (var t = 0; t < tokens.Count; t++)
        {
            var text = tokens[t];
            if (!FuzzyKeys.Eligible(text.Length) || (kinds[t] & WORDISH) == 0)
            {
                continue;
            }

            var count = FuzzyKeys.Keys(text, keys);
            for (var k = 0; k < count; k++)
            {
                // "settings" without either t is the same string; one pair is enough
                if (!keys[..k].Contains(keys[k]))
                {
                    pairs.Add(((ulong)keys[k] << 32) | (uint)rankOf[t]);
                }
            }
        }

        pairs.Sort();
        var keyColumn = new uint[pairs.Count];
        var tokenColumn = new uint[pairs.Count];
        for (var p = 0; p < pairs.Count; p++)
        {
            keyColumn[p] = (uint)(pairs[p] >> 32);
            tokenColumn[p] = (uint)pairs[p];
        }
        return (keyColumn, tokenColumn);
    }
}
