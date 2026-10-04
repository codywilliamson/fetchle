namespace Fetchle.Core.Naive;

// PLACEHOLDER: replaced by the lexical and semantic rankers
public sealed class SubstringRanker(string query)
{
    public const double NAME_MATCH = 1.0;
    public const double PATH_MATCH = 0.5;

    // 0 means no match. relativePath is the entry's path under its root
    public double Score(ReadOnlySpan<char> relativePath, ReadOnlySpan<char> name)
    {
        if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return NAME_MATCH;
        }

        if (relativePath.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return PATH_MATCH;
        }

        return 0;
    }
}
