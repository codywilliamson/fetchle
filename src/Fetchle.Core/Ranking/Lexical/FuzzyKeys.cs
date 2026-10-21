using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace Fetchle.Core.Ranking.Lexical;

// the deletion neighbourhood behind edit-distance-1 matching (symspell's trick). a token is keyed by its own hash
// and the hash of every copy with one char deleted; a query term looks up the same keys. two strings within one
// insert, delete, substitute or adjacent swap of each other always share a key, so fuzzy lookup is a handful of
// binary searches instead of a scan of the vocabulary. hashes can collide, so every candidate is verified
static class FuzzyKeys
{
    public const int MIN_LENGTH = 4;
    public const int MAX_LENGTH = 16;

    public static bool Eligible(int length) => length is >= MIN_LENGTH and <= MAX_LENGTH;

    public static uint Hash(ReadOnlySpan<char> text) => (uint)XxHash3.HashToUInt64(MemoryMarshal.AsBytes(text));

    // keys[0] is the whole text, then one per deleted position. returns how many were written
    public static int Keys(ReadOnlySpan<char> text, Span<uint> keys)
    {
        Span<char> deleted = stackalloc char[MAX_LENGTH];
        keys[0] = Hash(text);
        for (var i = 0; i < text.Length; i++)
        {
            text[..i].CopyTo(deleted);
            text[(i + 1)..].CopyTo(deleted[i..]);
            keys[i + 1] = Hash(deleted[..(text.Length - 1)]);
        }
        return text.Length + 1;
    }

    // restricted damerau-levenshtein distance is at most 1: one insert, delete, substitution or adjacent swap
    public static bool WithinOne(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        if (Math.Abs(a.Length - b.Length) > 1)
        {
            return false;
        }

        var prefix = a.CommonPrefixLength(b);
        if (prefix == a.Length && prefix == b.Length)
        {
            return true;
        }

        var restA = a[prefix..];
        var restB = b[prefix..];
        if (restA.Length == restB.Length)
        {
            // substitution, or a swap of the next two chars
            return restA[1..].SequenceEqual(restB[1..])
                || (restA.Length >= 2 && restA[0] == restB[1] && restA[1] == restB[0] && restA[2..].SequenceEqual(restB[2..]));
        }

        return restA.Length > restB.Length ? restA[1..].SequenceEqual(restB) : restB[1..].SequenceEqual(restA);
    }
}
