namespace Fetchle.Core.Ranking;

// every ranking knob in one place. each value was set on the dev split of the eval; evals/failures.md and
// docs/decisions.md say which measurement moved it
static class RankingWeights
{
    // lexical: what one token hit is worth to its term
    public const float EXACT_MATCH = 1f;
    public const float PREFIX_BASE = 0.5f;
    public const float PREFIX_COVERAGE = 0.4f;
    public const float FUZZY_MATCH = 0.6f;
    public const float EXTENSION_MATCH = 0.8f;
    // prefixes shorter than this only expand while typing in the picker
    public const int MIN_PREFIX = 3;
    // a prefix that opens more of the vocabulary than this is too vague to expand
    public const int MAX_PREFIX_TOKENS = 4096;

    // lexical: where in the path a term hit counts
    public const float ANCESTOR_MATCH = 0.5f;
    // each extra word in a name dilutes a hit in it a little, bm25's length normalization
    public const float NAME_LENGTH_PENALTY = 0.08f;
    // a path matching half the terms scores a quarter
    public const float COVERAGE_POWER = 2f;

    // fusion
    public const int RRF_K = 60;
    // candidates each retriever hands to fusion
    public const int CANDIDATES = 100;
    // per path segment below the search root
    public const float DEPTH_PENALTY = 0.01f;
}
