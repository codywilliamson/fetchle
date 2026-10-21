namespace Fetchle.Core.Ranking;

// a query's lexical terms: its lowercase alphanumeric chunks, in order. "NaiveWalker.cs" is naivewalker and cs;
// a camelCase chunk stays whole, because the lexicon keeps every split chunk whole as a compound token too.
// when a chunk finds nothing whole, its parts get their turn (see TermScores)
public sealed class QueryTerms
{
    public const int MAX_TERMS = 8;

    // words that say what kind of entry is wanted rather than what it's called: "the migrations folder"
    static readonly string[] DirectoryWords = ["folder", "folders", "directory", "directories", "dir", "dirs"];

    QueryTerms(string[] terms, string[][] parts, bool lastIsPrefix, bool wantsDirectory)
    {
        Terms = terms;
        Parts = parts;
        LastIsPrefix = lastIsPrefix;
        WantsDirectory = wantsDirectory;
    }

    // the query named the kind: a dir. the word itself isn't a term, since few dirs are called "folder"
    public bool WantsDirectory { get; }

    public string[] Terms { get; }

    // per term, its camelCase/digit parts when it has more than one
    public string[][] Parts { get; }

    // the picker re-ranks per keystroke, so the word being typed is a prefix even when it's short
    public bool LastIsPrefix { get; }

    public static QueryTerms Parse(string query, bool lastIsPrefix = false)
    {
        var terms = new List<string>(MAX_TERMS);
        var parts = new List<string[]>(MAX_TERMS);
        var i = 0;
        while (i < query.Length && terms.Count < MAX_TERMS)
        {
            while (i < query.Length && !char.IsLetterOrDigit(query[i]))
            {
                i++;
            }

            var start = i;
            while (i < query.Length && char.IsLetterOrDigit(query[i]))
            {
                i++;
            }

            if (i > start)
            {
                var chunk = query.AsSpan(start, i - start);
                var term = chunk.ToString().ToLowerInvariant();
                if (!terms.Contains(term))
                {
                    terms.Add(term);
                    parts.Add(SplitParts(chunk));
                }
            }
        }

        // a trailing separator means the last word is finished
        var endsInWord = query.Length > 0 && char.IsLetterOrDigit(query[^1]);
        DropStopwords(terms, parts, keepLast: lastIsPrefix && endsInWord);
        var wantsDirectory = DropDirectoryWords(terms, parts, keepLast: lastIsPrefix && endsInWord);
        return new QueryTerms([.. terms], [.. parts], lastIsPrefix && endsInWord, wantsDirectory);
    }

    // a query of nothing but stopwords keeps them, it's all there is to go on. the word still being typed stays too:
    // "the" may be the start of "theme"
    static void DropStopwords(List<string> terms, List<string[]> parts, bool keepLast)
    {
        if (terms.TrueForAll(Stopwords.Contains))
        {
            return;
        }

        for (var t = terms.Count - (keepLast ? 2 : 1); t >= 0; t--)
        {
            if (Stopwords.Contains(terms[t]))
            {
                terms.RemoveAt(t);
                parts.RemoveAt(t);
            }
        }
    }

    // like stopwords, but noted: true when one was there. a query that is only the word keeps it
    static bool DropDirectoryWords(List<string> terms, List<string[]> parts, bool keepLast)
    {
        var found = false;
        for (var t = terms.Count - (keepLast ? 2 : 1); t >= 0 && terms.Count > 1; t--)
        {
            if (Array.IndexOf(DirectoryWords, terms[t]) >= 0)
            {
                terms.RemoveAt(t);
                parts.RemoveAt(t);
                found = true;
            }
        }
        return found;
    }

    static string[] SplitParts(ReadOnlySpan<char> chunk)
    {
        Span<char> lowered = stackalloc char[chunk.Length];
        Span<Tokens.Token> tokens = stackalloc Tokens.Token[MAX_TERMS * 2];
        var count = Tokens.SegmentTokenizer.Tokenize(chunk, isFile: false, lowered, tokens);
        var parts = new List<string>();
        for (var t = 0; t < count; t++)
        {
            if (tokens[t].Kind is Tokens.TokenKind.Word or Tokens.TokenKind.Number)
            {
                parts.Add(new string(tokens[t].In(lowered)));
            }
        }
        return parts.Count > 1 ? [.. parts] : [];
    }
}
