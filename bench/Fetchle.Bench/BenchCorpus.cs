using Fetchle.Fixtures;

namespace Fetchle.Bench;

// the two corpus shapes every benchmark runs against
public static class BenchCorpus
{
    public const int FILLER = 20_000;
    public const string REALISTIC = "realistic";
    public const string DIR_HEAVY = "dir-heavy";

    public static readonly string[] Shapes = [REALISTIC, DIR_HEAVY];

    public static string Ensure(string shape)
    {
        var filesPerDirectory = shape == DIR_HEAVY ? Corpus.DIR_HEAVY_FILES_PER_DIRECTORY : Corpus.REALISTIC_FILES_PER_DIRECTORY;
        return Corpus.Ensure(Corpus.DefaultRoot(FILLER, filesPerDirectory), FILLER, filesPerDirectory);
    }
}
