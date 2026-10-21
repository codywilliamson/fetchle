using System.Collections.Frozen;

namespace Fetchle.Core.Ranking;

// words a person puts in a question that never name a file: "rules for skipping folders during the walk". as lexical
// terms they cost coverage and match junk ("for", "the" sit inside plenty of names), so they're dropped there. the
// embedder still reads the whole query
static class Stopwords
{
    static readonly FrozenSet<string> Words = new[]
    {
        "a", "an", "the", "of", "for", "to", "in", "on", "at", "by", "with", "from", "into", "about", "during",
        "and", "or", "that", "which", "who", "where", "what", "when", "how", "this", "these", "those", "it", "its",
        "is", "are", "was", "be", "my", "me", "i", "our", "your", "s", "some", "any", "all",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static bool Contains(string word) => Words.Contains(word);
}
