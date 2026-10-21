namespace Fetchle.Core.Ranking.Lexical;

// the token side of a snapshot, read in place:
//   tokens       sorted by utf-8 bytes: offsets (count + 1) into a blob, no terminators
//   postings     per token rank, the segments holding it (Posting.Pack), ascending: offsets (count + 1) into one column
//   fuzzy        (key, token rank) pairs sorted by key, see FuzzyKeys
//   word counts  per segment, how many words its name split into, for length normalization
readonly ref struct Lexicon
{
    readonly ReadOnlySpan<uint> _tokenOffsets;
    readonly ReadOnlySpan<byte> _tokenBlob;
    readonly ReadOnlySpan<uint> _postingOffsets;
    readonly ReadOnlySpan<uint> _postings;
    readonly ReadOnlySpan<uint> _fuzzyKeys;
    readonly ReadOnlySpan<uint> _fuzzyTokens;

    public Lexicon(ReadOnlySpan<uint> tokenOffsets, ReadOnlySpan<byte> tokenBlob, ReadOnlySpan<uint> postingOffsets, ReadOnlySpan<uint> postings,
        ReadOnlySpan<uint> fuzzyKeys, ReadOnlySpan<uint> fuzzyTokens, ReadOnlySpan<byte> segmentWordCounts)
    {
        _tokenOffsets = tokenOffsets;
        _tokenBlob = tokenBlob;
        _postingOffsets = postingOffsets;
        _postings = postings;
        _fuzzyKeys = fuzzyKeys;
        _fuzzyTokens = fuzzyTokens;
        SegmentWordCounts = segmentWordCounts;
    }

    public int TokenCount => _tokenOffsets.Length - 1;

    public int PostingCount => _postings.Length;

    public int FuzzyKeyCount => _fuzzyKeys.Length;

    public ReadOnlySpan<byte> SegmentWordCounts { get; }

    public ReadOnlySpan<byte> Token(int rank) => _tokenBlob[(int)_tokenOffsets[rank]..(int)_tokenOffsets[rank + 1]];

    public ReadOnlySpan<uint> Postings(int rank) => _postings[(int)_postingOffsets[rank]..(int)_postingOffsets[rank + 1]];

    // -1 when the token isn't in the vocabulary
    public int Find(ReadOnlySpan<byte> token)
    {
        var rank = LowerBound(token);
        return rank < TokenCount && Token(rank).SequenceEqual(token) ? rank : -1;
    }

    // ranks [Start, End) of every token starting with prefix, the token equal to it included
    public (int Start, int End) PrefixRange(ReadOnlySpan<byte> prefix)
    {
        var start = LowerBound(prefix);
        int low = start, high = TokenCount;
        while (low < high)
        {
            var mid = (low + high) >>> 1;
            if (Token(mid).StartsWith(prefix))
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        return (start, low);
    }

    // token ranks stored under one fuzzy key
    public ReadOnlySpan<uint> FuzzyTokens(uint key)
    {
        var start = _fuzzyKeys.BinarySearch(key);
        if (start < 0)
        {
            return [];
        }

        // BinarySearch lands on any equal key; widen to the whole run
        while (start > 0 && _fuzzyKeys[start - 1] == key)
        {
            start--;
        }

        var end = start + 1;
        while (end < _fuzzyKeys.Length && _fuzzyKeys[end] == key)
        {
            end++;
        }
        return _fuzzyTokens[start..end];
    }

    int LowerBound(ReadOnlySpan<byte> key)
    {
        int low = 0, high = TokenCount;
        while (low < high)
        {
            var mid = (low + high) >>> 1;
            if (Token(mid).SequenceCompareTo(key) < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        return low;
    }
}
