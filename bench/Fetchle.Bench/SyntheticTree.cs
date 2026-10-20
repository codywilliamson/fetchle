using Fetchle.Core.Index;

namespace Fetchle.Bench;

// a home-dir-shaped tree built straight into collectors, no disk: heavy-tailed files per dir, mixed depth,
// a few "settings" files. deterministic from the seed, so every run benches the same snapshot
static class SyntheticTree
{
    const int SEED = 42;
    const string ROOT = "/bench/home";

    static readonly string[] Words = ["alpha", "build", "cache", "delta", "event", "frame", "graph", "index", "layer", "model", "parse", "query", "route", "store", "token", "video", "Shared", "Config", "Profiles", "Extensions"];
    static readonly string[] Extensions = [".json", ".log", ".dll", ".png", ".js", ".css", ".txt", ".xml", ".dat", ".db", ".cs", ".md", ".jpg", ""];

    public static PathTree Build(int entries)
    {
        var rng = new Random(SEED);
        var collector = new TreeCollector();
        var queue = new Queue<string>();
        queue.Enqueue(ROOT);
        var made = 0;
        while (queue.TryDequeue(out var dir) && made < entries)
        {
            // most dirs hold a handful of files, a few hold thousands
            var files = (int)Math.Min(4000, Math.Exp(rng.NextDouble() * rng.NextDouble() * 9));
            var subdirs = queue.Count < 64 ? rng.Next(2, 8) : rng.Next(0, 4);
            for (var f = 0; f < files && made < entries; f++, made++)
            {
                var name = rng.Next(50_000) == 0 ? "settings.json" : $"{Words[rng.Next(Words.Length)]}_{made:D7}{Extensions[rng.Next(Extensions.Length)]}";
                collector.Add(dir, name, isDirectory: false);
            }

            for (var d = 0; d < subdirs && made < entries; d++, made++)
            {
                var name = $"{Words[rng.Next(Words.Length)]}{d}";
                collector.Add(dir, name, isDirectory: true);
                queue.Enqueue(Path.Join(dir, name));
            }
        }

        return TreeAssembler.Assemble([new RootWalk(ROOT, [collector], TimeSpan.Zero)]);
    }

    public static string Root => ROOT;
}
