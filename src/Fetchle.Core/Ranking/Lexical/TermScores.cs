using System.Buffers;
using System.Text;

namespace Fetchle.Core.Ranking.Lexical;

// how well each unique segment matches each query term, packed one byte per term into a ulong per segment, so the
// entry pass reads one value per entry. a term expands to the tokens it hits exactly, as a prefix and within one
// edit, each with a weight; the term's postings for those tokens are the only segments it ever touches.
// nothing here looks at a segment the lexicon didn't point to
sealed class TermScores : IDisposable
{
    public const int MAX_TERMS = QueryTerms.MAX_TERMS;
    const int MAX_TOKEN_BYTES = 256;

    TermScores(ulong[] packed, int segmentCount, float[] weights, int termCount)
    {
        Packed = packed;
        SegmentCount = segmentCount;
        Weights = weights;
        TermCount = termCount;
    }

    // per segment, byte t is term t's match strength, 0 to 255. rented, longer than SegmentCount
    public ulong[] Packed { get; }

    public int SegmentCount { get; }

    // per term, its share of the query's idf. sums to 1 when any term matched
    public float[] Weights { get; }

    public int TermCount { get; }

    public static TermScores Build(Lexicon lexicon, int segmentCount, QueryTerms query)
    {
        var packed = ArrayPool<ulong>.Shared.Rent(Math.Max(segmentCount, 1));
        packed.AsSpan(0, segmentCount).Clear();
        var terms = EffectiveTerms(lexicon, query);
        var weights = new float[MAX_TERMS];
        var matches = new List<(int Rank, float Weight)>();
        var total = 0f;
        for (var t = 0; t < terms.Count; t++)
        {
            matches.Clear();
            Expand(lexicon, terms[t].Text, terms[t].Prefix, matches);
            var touched = 0;
            var shift = t * 8;
            foreach (var (rank, weight) in matches)
            {
                foreach (var posting in lexicon.Postings(rank))
                {
                    var segment = Posting.Segment(posting);
                    var strength = (ulong)(weight * KindWeight(Posting.Kind(posting)) * byte.MaxValue);
                    var current = (packed[segment] >> shift) & 0xFF;
                    if (current == 0)
                    {
                        touched++;
                    }

                    if (strength > current)
                    {
                        packed[segment] = (packed[segment] & ~(0xFFUL << shift)) | (strength << shift);
                    }
                }
            }

            // rare terms decide more than common ones; a term nothing matched carries no weight
            weights[t] = touched == 0 ? 0 : MathF.Log(1 + (float)segmentCount / touched);
            total += weights[t];
        }

        if (total > 0)
        {
            for (var t = 0; t < terms.Count; t++)
            {
                weights[t] /= total;
            }
        }
        return new TermScores(packed, segmentCount, weights, terms.Count);
    }

    static float KindWeight(uint kind) => kind == Posting.EXTENSION ? RankingWeights.EXTENSION_MATCH : 1f;

    readonly record struct Term(string Text, bool Prefix);

    // a camelCase or digit-joined term that hits nothing whole is searched as its parts
    static List<Term> EffectiveTerms(Lexicon lexicon, QueryTerms query)
    {
        var terms = new List<Term>(MAX_TERMS);
        var probe = new List<(int, float)>();
        for (var t = 0; t < query.Terms.Length && terms.Count < MAX_TERMS; t++)
        {
            var prefix = query.LastIsPrefix && t == query.Terms.Length - 1;
            probe.Clear();
            Expand(lexicon, query.Terms[t], prefix, probe);
            if (probe.Count > 0 || query.Parts[t].Length == 0)
            {
                terms.Add(new Term(query.Terms[t], prefix));
                continue;
            }

            for (var p = 0; p < query.Parts[t].Length && terms.Count < MAX_TERMS; p++)
            {
                terms.Add(new Term(query.Parts[t][p], prefix && p == query.Parts[t].Length - 1));
            }
        }
        return terms;
    }

    // every token the term hits, best weight per token: exact 1, prefix by how much of the token it covers,
    // fuzzy a flat discount
    static void Expand(Lexicon lexicon, string term, bool typingPrefix, List<(int Rank, float Weight)> matches)
    {
        Span<byte> utf8 = stackalloc byte[MAX_TOKEN_BYTES];
        if (Encoding.UTF8.GetByteCount(term) > MAX_TOKEN_BYTES)
        {
            return;
        }

        var bytes = utf8[..Encoding.UTF8.GetBytes(term, utf8)];
        var exact = lexicon.Find(bytes);
        if (exact >= 0)
        {
            matches.Add((exact, RankingWeights.EXACT_MATCH));
        }

        if (term.Length >= RankingWeights.MIN_PREFIX || typingPrefix)
        {
            var (start, end) = lexicon.PrefixRange(bytes);
            if (end - start <= RankingWeights.MAX_PREFIX_TOKENS)
            {
                for (var rank = start; rank < end; rank++)
                {
                    if (rank != exact)
                    {
                        var coverage = (float)bytes.Length / lexicon.Token(rank).Length;
                        matches.Add((rank, RankingWeights.PREFIX_BASE + RankingWeights.PREFIX_COVERAGE * coverage));
                    }
                }
            }
        }

        if (FuzzyKeys.Eligible(term.Length) && HasLetter(term))
        {
            AddFuzzy(lexicon, term, matches);
        }
    }

    static void AddFuzzy(Lexicon lexicon, string term, List<(int Rank, float Weight)> matches)
    {
        Span<uint> keys = stackalloc uint[FuzzyKeys.MAX_LENGTH + 1];
        Span<char> decoded = stackalloc char[MAX_TOKEN_BYTES];
        var count = FuzzyKeys.Keys(term, keys);
        for (var k = 0; k < count; k++)
        {
            foreach (var candidate in lexicon.FuzzyTokens(keys[k]))
            {
                var rank = (int)candidate;
                if (Contains(matches, rank))
                {
                    continue;
                }

                var token = lexicon.Token(rank);
                if (token.Length > MAX_TOKEN_BYTES)
                {
                    continue;
                }

                var chars = decoded[..Encoding.UTF8.GetChars(token, decoded)];
                if (FuzzyKeys.WithinOne(term, chars))
                {
                    matches.Add((rank, RankingWeights.FUZZY_MATCH));
                }
            }
        }
    }

    static bool Contains(List<(int Rank, float Weight)> matches, int rank)
    {
        foreach (var match in matches)
        {
            if (match.Rank == rank)
            {
                return true;
            }
        }
        return false;
    }

    static bool HasLetter(string term)
    {
        foreach (var c in term)
        {
            if (char.IsLetter(c))
            {
                return true;
            }
        }
        return false;
    }

    public void Dispose() => ArrayPool<ulong>.Shared.Return(Packed);
}
