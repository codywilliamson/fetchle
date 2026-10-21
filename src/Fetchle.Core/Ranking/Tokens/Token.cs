namespace Fetchle.Core.Ranking.Tokens;

public enum TokenKind : byte
{
    Word,
    Number,
    // the last chunk after a file name's final dot
    Extension,
    // a whole camelCase or alphanumeric chunk that also split into parts: "githubdesktop" beside git hub desktop,
    // so a query spelled as one word still finds it. the embedder skips these, the lexicon keeps them
    Compound,
    // a hash, guid, publisher id or random id kept whole, never split. only an exact query match finds it;
    // the embedder and the fuzzy keys skip it
    Noise,
}

// one lowercase word inside a segment's lowered copy
public readonly record struct Token(int Start, int Length, TokenKind Kind)
{
    public ReadOnlySpan<char> In(ReadOnlySpan<char> lowered) => lowered.Slice(Start, Length);
}
