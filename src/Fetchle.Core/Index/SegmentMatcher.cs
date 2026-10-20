using System.Buffers;
using System.Text;

namespace Fetchle.Core.Index;

// marks every unique name segment that contains the query, case-insensitively, the way
// string.Contains(query, OrdinalIgnoreCase) would on the decoded name. an ascii query scans the whole utf-8 blob
// in one vectorized pass; only segments with non-ascii bytes get decoded
static class SegmentMatcher
{
    const int BITS = 64;

    // one bit per segment. the caller returns the array to ArrayPool<ulong>.Shared
    public static ulong[] Match(Snapshot snapshot, string query, out bool any)
    {
        var words = (snapshot.SegmentCount + BITS - 1) / BITS;
        var bits = ArrayPool<ulong>.Shared.Rent(Math.Max(words, 1));
        bits.AsSpan().Clear();
        any = false;

        // names never hold a NUL, and the blob's NULs are terminators that mustn't match
        if (query.Contains('\0'))
        {
            return bits;
        }

        if (query.Length == 0)
        {
            bits.AsSpan(0, words).Fill(ulong.MaxValue);
            any = snapshot.SegmentCount > 0;
            return bits;
        }

        if (Ascii.IsValid(query))
        {
            any = ScanAscii(snapshot, query, bits);
            // ascii bytes can't see a non-ascii name that folds to the query, so those get decoded either way
            foreach (var segment in snapshot.NonAsciiSegments)
            {
                any |= Set(bits, segment, DecodedContains(snapshot, segment, query));
            }
            return bits;
        }

        for (var segment = 0u; segment < (uint)snapshot.SegmentCount; segment++)
        {
            any |= Set(bits, segment, DecodedContains(snapshot, segment, query));
        }
        return bits;
    }

    public static bool IsSet(ulong[] bits, uint segment) => (bits[segment / BITS] & (1UL << (int)(segment % BITS))) != 0;

    static bool ScanAscii(Snapshot snapshot, string query, ulong[] bits)
    {
        const int STACK_NEEDLE = 256;
        var needle = query.Length <= STACK_NEEDLE ? stackalloc byte[STACK_NEEDLE] : new byte[query.Length];
        needle = needle[..Encoding.ASCII.GetBytes(query, needle)];

        var blob = snapshot.SegmentBlob;
        var offsets = snapshot.SegmentOffsets;
        var any = false;
        var position = 0;
        while (position < blob.Length)
        {
            var found = AsciiFinder.IndexOf(blob[position..], needle);
            if (found < 0)
            {
                break;
            }

            var segment = SegmentAt(offsets, position + found);
            bits[segment / BITS] |= 1UL << (int)(segment % BITS);
            any = true;
            // one hit is enough for a segment, carry on from the next one
            position = (int)offsets[(int)segment + 1];
        }
        return any;
    }

    // the segment whose bytes hold blob position, by binary search on the start offsets
    static uint SegmentAt(ReadOnlySpan<uint> offsets, int position)
    {
        int low = 0, high = offsets.Length - 2;
        while (low < high)
        {
            var mid = (low + high + 1) >>> 1;
            if (offsets[mid] <= (uint)position)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }
        return (uint)low;
    }

    static bool DecodedContains(Snapshot snapshot, uint segment, string query)
    {
        var bytes = snapshot.Segment(segment);
        char[]? rented = null;
        var chars = bytes.Length <= 512 ? stackalloc char[bytes.Length] : (rented = ArrayPool<char>.Shared.Rent(bytes.Length));
        var count = Encoding.UTF8.GetChars(bytes, chars);
        var contains = chars[..count].Contains(query, StringComparison.OrdinalIgnoreCase);
        if (rented is not null)
        {
            ArrayPool<char>.Shared.Return(rented);
        }
        return contains;
    }

    static bool Set(ulong[] bits, uint segment, bool value)
    {
        var mask = 1UL << (int)(segment % BITS);
        bits[segment / BITS] = value ? bits[segment / BITS] | mask : bits[segment / BITS] & ~mask;
        return value;
    }
}
