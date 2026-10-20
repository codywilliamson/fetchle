using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;

namespace Fetchle.Core.Index;

// case-insensitive search for an ascii needle in bytes. a vector pass compares the needle's first and last bytes
// at every position at once (letters folded with | 0x20), so only spots where both line up get a full compare.
// matching on the first byte alone stopped at every 's' for "settings"
static class AsciiFinder
{
    const byte CASE_BIT = 0x20;

    public static int IndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.IsEmpty)
        {
            return 0;
        }

        var lastStart = haystack.Length - needle.Length;
        if (lastStart < 0)
        {
            return -1;
        }

        var foldFirst = IsLetter(needle[0]) ? CASE_BIT : (byte)0;
        var foldLast = IsLetter(needle[^1]) ? CASE_BIT : (byte)0;
        var first = (byte)(needle[0] | foldFirst);
        var last = (byte)(needle[^1] | foldLast);
        var at = 0;

        if (Vector256.IsHardwareAccelerated && lastStart >= Vector256<byte>.Count)
        {
            var firsts = Vector256.Create(first);
            var lasts = Vector256.Create(last);
            var foldFirsts = Vector256.Create(foldFirst);
            var foldLasts = Vector256.Create(foldLast);
            for (; at <= lastStart - Vector256<byte>.Count + 1; at += Vector256<byte>.Count)
            {
                var a = Vector256.Create(haystack.Slice(at, Vector256<byte>.Count)) | foldFirsts;
                var b = Vector256.Create(haystack.Slice(at + needle.Length - 1, Vector256<byte>.Count)) | foldLasts;
                var candidates = (Vector256.Equals(a, firsts) & Vector256.Equals(b, lasts)).ExtractMostSignificantBits();
                while (candidates != 0)
                {
                    var position = at + BitOperations.TrailingZeroCount(candidates);
                    if (Ascii.EqualsIgnoreCase(haystack.Slice(position, needle.Length), needle))
                    {
                        return position;
                    }
                    candidates &= candidates - 1;
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated && lastStart >= Vector128<byte>.Count)
        {
            var firsts = Vector128.Create(first);
            var lasts = Vector128.Create(last);
            var foldFirsts = Vector128.Create(foldFirst);
            var foldLasts = Vector128.Create(foldLast);
            for (; at <= lastStart - Vector128<byte>.Count + 1; at += Vector128<byte>.Count)
            {
                var a = Vector128.Create(haystack.Slice(at, Vector128<byte>.Count)) | foldFirsts;
                var b = Vector128.Create(haystack.Slice(at + needle.Length - 1, Vector128<byte>.Count)) | foldLasts;
                var candidates = (Vector128.Equals(a, firsts) & Vector128.Equals(b, lasts)).ExtractMostSignificantBits();
                while (candidates != 0)
                {
                    var position = at + BitOperations.TrailingZeroCount(candidates);
                    if (Ascii.EqualsIgnoreCase(haystack.Slice(position, needle.Length), needle))
                    {
                        return position;
                    }
                    candidates &= candidates - 1;
                }
            }
        }

        for (; at <= lastStart; at++)
        {
            if ((haystack[at] | foldFirst) == first && Ascii.EqualsIgnoreCase(haystack.Slice(at, needle.Length), needle))
            {
                return at;
            }
        }
        return -1;
    }

    static bool IsLetter(byte b) => (uint)((b | CASE_BIT) - 'a') <= 'z' - 'a';
}
