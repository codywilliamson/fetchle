using Microsoft.Win32.SafeHandles;

namespace Fetchle.Fixtures;

// turns a plan into empty files on disk, one parallel task per dir
static class HomeCorpusWriter
{
    const int RNG_STRIDE = 1_000_003;

    public static void Write(HomePlan plan, string root, int seed)
    {
        Directory.CreateDirectory(root);
        Parallel.For(0, plan.Paths.Count, index => WriteDir(plan, root, seed, index));
    }

    static void WriteDir(HomePlan plan, string root, int seed, int index)
    {
        var dir = Path.Combine(root, plan.Paths[index].Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(dir);

        var fixedNames = plan.FixedFiles.GetValueOrDefault(index) ?? [];
        var count = plan.GeneratedFiles[index];
        if (fixedNames.Count + count == 0)
        {
            return;
        }

        // per-dir rng so the result doesn't depend on which thread got which dir
        var rng = new Random(unchecked(seed * RNG_STRIDE + index));
        var generated = HomeFileNames.For(plan.Kinds[index], count, rng, fixedNames);
        foreach (var name in fixedNames.Concat(generated))
        {
            Touch(Path.Combine(dir, name));
        }
    }

    static void Touch(string path)
    {
        using SafeFileHandle handle = File.OpenHandle(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    }
}
