namespace Fetchle.Core.Ranking.Tokens;

// decides which alphanumeric chunks of a name are machine noise: hashes, guids, package-family publisher ids,
// random ids and long numbers. noise tokens never reach the lexicon or the embedder; in the spike,
// "8 wekyb 3 d 8 bbwe" from Microsoft.WindowsTerminal_8wekyb3d8bbwe pushed the real settings past rank 7,000
static class NoiseFilter
{
    // hex runs this long with a digit in them are hashes: git objects, content hashes, build ids
    const int MIN_HEX_HASH = 8;
    // hex this long is a hash even without digits
    const int MIN_HEX_ANY = 16;
    // random ids alternate letters and digits far more often than words do ("k2x9qv1a", "8wekyb3d8bbwe")
    const int MIN_RANDOM_LENGTH = 6;
    const int MIN_RANDOM_TRANSITIONS = 3;
    // timestamps, epoch ms, build numbers
    const int MIN_NOISE_NUMBER = 7;
    // base64 and other opaque blobs
    const int MIN_OPAQUE_LENGTH = 20;
    // a windows package family name ends in "_" plus a 13 char publisher id
    const int PUBLISHER_ID_LENGTH = 13;

    static readonly int[] GuidGroups = [8, 4, 4, 4, 12];

    public static bool IsNoise(ReadOnlySpan<char> chunk)
    {
        var digits = 0;
        var hex = 0;
        var transitions = 0;
        var mixedCase = 0;
        for (var i = 0; i < chunk.Length; i++)
        {
            var c = chunk[i];
            if (char.IsAsciiDigit(c))
            {
                digits++;
            }

            if (char.IsAsciiHexDigit(c))
            {
                hex++;
            }

            if (i > 0)
            {
                var previous = chunk[i - 1];
                if (char.IsAsciiDigit(c) != char.IsAsciiDigit(previous))
                {
                    transitions++;
                }

                if (char.IsUpper(c) && char.IsLower(previous))
                {
                    mixedCase++;
                }
            }
        }

        if (hex == chunk.Length && (chunk.Length >= MIN_HEX_ANY || (chunk.Length >= MIN_HEX_HASH && digits > 0)))
        {
            return true;
        }

        if (digits == chunk.Length)
        {
            return chunk.Length >= MIN_NOISE_NUMBER;
        }

        if (chunk.Length >= MIN_RANDOM_LENGTH && digits > 0 && transitions >= MIN_RANDOM_TRANSITIONS)
        {
            return true;
        }

        // camelCase words have a few humps; opaque blobs have digits and humps everywhere
        return chunk.Length >= MIN_OPAQUE_LENGTH && digits > 0 && mixedCase >= 3;
    }

    // "Publisher.App_8wekyb3d8bbwe": the chunk is the segment's last, follows "_", is 13 lowercase
    // alphanumerics, and a dot comes earlier in the segment
    public static bool IsPublisherId(ReadOnlySpan<char> segment, int start, int length)
    {
        if (length != PUBLISHER_ID_LENGTH || start + length != segment.Length || start == 0 || segment[start - 1] != '_')
        {
            return false;
        }

        foreach (var c in segment.Slice(start, length))
        {
            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c))
            {
                return false;
            }
        }
        return segment[..start].Contains('.');
    }

    // a guid spelled with dashes starts at chunk `first`: five hex chunks of 8-4-4-4-12, joined by single dashes
    public static bool IsGuidAt(ReadOnlySpan<char> segment, ReadOnlySpan<(int Start, int Length)> chunks, int first)
    {
        if (first + GuidGroups.Length > chunks.Length)
        {
            return false;
        }

        for (var g = 0; g < GuidGroups.Length; g++)
        {
            var (start, length) = chunks[first + g];
            if (length != GuidGroups[g] || !IsHex(segment.Slice(start, length)))
            {
                return false;
            }

            if (g > 0)
            {
                var (previousStart, previousLength) = chunks[first + g - 1];
                if (start != previousStart + previousLength + 1 || segment[start - 1] != '-')
                {
                    return false;
                }
            }
        }
        return true;
    }

    static bool IsHex(ReadOnlySpan<char> chunk)
    {
        foreach (var c in chunk)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }
        return true;
    }
}
