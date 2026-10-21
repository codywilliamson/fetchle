using Fetchle.Core.Ranking.Tokens;

namespace Fetchle.Core.Ranking.Lexical;

// one posting is a segment id with how the token appeared in it, in the low two bits
static class Posting
{
    public const int KIND_BITS = 2;
    public const uint MAX_SEGMENTS = uint.MaxValue >> KIND_BITS;

    public const uint WORD = 0;
    public const uint NUMBER = 1;
    public const uint EXTENSION = 2;
    public const uint NOISE = 3;

    public static uint Pack(uint segment, TokenKind kind) => (segment << KIND_BITS) | kind switch
    {
        TokenKind.Number => NUMBER,
        TokenKind.Extension => EXTENSION,
        TokenKind.Noise => NOISE,
        _ => WORD,
    };

    public static uint Segment(uint posting) => posting >> KIND_BITS;

    public static uint Kind(uint posting) => posting & ((1u << KIND_BITS) - 1);
}
