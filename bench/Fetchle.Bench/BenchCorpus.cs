using Fetchle.Fixtures;

namespace Fetchle.Bench;

// the corpus shapes the benchmarks run against
public static class BenchCorpus
{
    public const int FILLER = 20_000;
    public const string REALISTIC = "realistic";
    public const string DIR_HEAVY = "dir-heavy";
    public const string HOME_1M = "home-1m";

    // the shape label for results that don't touch a corpus, like the process startup baselines
    public const string NONE = "none";

    // BenchmarkDotNet params. home-1m is too slow to sweep there, it's external-only
    public static readonly string[] Shapes = [REALISTIC, DIR_HEAVY];

    public static readonly string[] ExternalShapes = [REALISTIC, DIR_HEAVY, HOME_1M];

    public static string Ensure(string shape)
    {
        if (shape == HOME_1M)
        {
            var root = HomeCorpus.DefaultRoot();
            HomeCorpus.Ensure(root);
            return root;
        }

        var filesPerDirectory = shape == DIR_HEAVY ? Corpus.DIR_HEAVY_FILES_PER_DIRECTORY : Corpus.REALISTIC_FILES_PER_DIRECTORY;
        return Corpus.Ensure(Corpus.DefaultRoot(FILLER, filesPerDirectory), FILLER, filesPerDirectory);
    }
}
