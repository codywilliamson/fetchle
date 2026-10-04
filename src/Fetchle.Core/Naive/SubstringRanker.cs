namespace Fetchle.Core.Naive;

// PLACEHOLDER: plain case-insensitive substring match. the lexical and semantic
// retrievers fused by rrf replace this, see docs/architecture.md#ranking-a-query
public sealed class SubstringRanker(string query)
{
    public const double NameMatch = 1.0;
    public const double PathMatch = 0.5;

    // 0 means no match. relativePath is the entry's path under its root
    public double Score(ReadOnlySpan<char> relativePath, ReadOnlySpan<char> name)
    {
        if (name.Contains(query, StringComparison.OrdinalIgnoreCase)) return NameMatch;
        if (relativePath.Contains(query, StringComparison.OrdinalIgnoreCase)) return PathMatch;
        return 0;
    }
}
