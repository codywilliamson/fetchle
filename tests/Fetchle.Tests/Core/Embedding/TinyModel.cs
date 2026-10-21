using Fetchle.Core.Embedding;

namespace Fetchle.Tests.Core.Embedding;

// a hand-made 4-dim model so tokenizer and encoder behavior is checked without the 33 MB real one.
// every row is a unit vector except the punctuation and cjk ones, which is what makes the expected vectors easy to write
static class TinyModel
{
    public const int PAD = 0;
    public const int UNK = 1;
    public const int PLAY = 2;
    public const int ING = 3;
    public const int ED = 4;
    public const int CAFE = 5;
    public const int DOT = 6;
    public const int DASH = 7;
    public const int RI = 8;
    public const int BEN = 9;
    public const int E = 10;
    public const int PLAYING = 11;

    static readonly string[] Tokens = ["[PAD]", "[UNK]", "play", "##ing", "##ed", "cafe", ".", "-", "日", "本", "e", "playing"];

    static readonly float[][] Rows =
    [
        [0, 0, 0, 0],
        [9, 9, 9, 9],
        [1, 0, 0, 0],
        [0, 1, 0, 0],
        [0, 0, 1, 0],
        [0, 0, 0, 1],
        [1, 1, 0, 0],
        [0, 1, 1, 0],
        [0, 0, 1, 1],
        [1, 0, 0, 1],
        [0.5f, 0.5f, 0.5f, 0.5f],
        [1, 1, 1, 1],
    ];

    public static byte[] Image(int maxWordChars = ModelWriter.DEFAULT_MAX_WORD_CHARS) =>
        ModelWriter.Build(Tokens, Rows.SelectMany(r => r).ToArray(), 4, UNK, "##", maxWordChars);

    public static StaticModel Open(int maxWordChars = ModelWriter.DEFAULT_MAX_WORD_CHARS) => StaticModel.FromBytes(Image(maxWordChars));

    // the f32 vector the f32 weights would give: normalize(sum of rows)
    public static float[] Expected(params int[] ids)
    {
        var sum = new float[4];
        foreach (var id in ids)
        {
            for (var d = 0; d < 4; d++)
            {
                sum[d] += Rows[id][d];
            }
        }

        var norm = MathF.Sqrt(sum.Sum(v => v * v));
        return [.. sum.Select(v => v / norm)];
    }
}
