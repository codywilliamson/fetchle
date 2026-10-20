namespace Fetchle.Fixtures;

// a .git dir: two-hex fan-out under objects/ holding loose objects, plus the few small dirs git always has
static class GitGrower
{
    const int FAN_OUT = 256;
    const int MIN_FAN_OUT = 4;
    const int ENTRIES_PER_FAN_OUT_DIR = 20;

    static readonly string[] SmallDirs = ["hooks", "info", "refs/heads", "refs/tags", "refs/remotes/origin", "logs/refs/heads"];
    static readonly string[] TopFiles = ["HEAD", "config", "description", "index", "COMMIT_EDITMSG", "packed-refs"];

    public static void Grow(HomePlan plan, string gitPath, int budget, Random rng)
    {
        var start = plan.Entries;
        var root = plan.AddDir(gitPath, FileKind.Data);
        foreach (var file in TopFiles)
        {
            plan.AddFixedFile(root, file);
        }
        foreach (var small in SmallDirs)
        {
            plan.AddDir($"{gitPath}/{small}", FileKind.Data);
        }

        var fanOut = Math.Clamp(budget / ENTRIES_PER_FAN_OUT_DIR, MIN_FAN_OUT, FAN_OUT);
        var order = Enumerable.Range(0, FAN_OUT).OrderBy(_ => rng.Next()).Take(fanOut).ToArray();
        var dirs = order.Select(n => plan.AddDir($"{gitPath}/objects/{n:x2}", FileKind.GitObject)).ToArray();

        var objects = Math.Clamp(budget - (plan.Entries - start), 0, plan.Remaining);
        var weights = dirs.Select(_ => -Math.Log(1 - rng.NextDouble())).ToArray();
        var weightSum = weights.Sum();
        var placed = 0;
        for (var i = 0; i < dirs.Length; i++)
        {
            var share = i == dirs.Length - 1 ? objects - placed : (int)(objects * weights[i] / weightSum);
            plan.AddGeneratedFiles(dirs[i], Math.Max(0, Math.Min(share, plan.Remaining)));
            placed += share;
        }
    }
}
