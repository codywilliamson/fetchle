using Fetchle.Fixtures;

namespace Fetchle.Bench;

// the two corpus shapes every benchmark runs against
public static class BenchCorpus
{
    public const int Filler = 20_000;
    public const string Realistic = "realistic";
    public const string DirHeavy = "dir-heavy";

    public static readonly string[] Shapes = [Realistic, DirHeavy];

    public static string Ensure(string shape)
    {
        var filesPerDirectory = shape == DirHeavy ? Corpus.DirHeavyFilesPerDirectory : Corpus.RealisticFilesPerDirectory;
        return Corpus.Ensure(Corpus.DefaultRoot(Filler, filesPerDirectory), Filler, filesPerDirectory);
    }
}
