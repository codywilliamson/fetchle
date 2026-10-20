namespace Fetchle.Fixtures;

// grows one subtree by random recursive attachment: each new dir hangs off an earlier one,
// which gives mostly shallow trees with a long tail of deep chains
static class TreeGrower
{
    const double DEEP_CHAIN_CHANCE = 0.01;
    const int DEEP_CHAIN_MIN = 5;
    const int DEEP_CHAIN_MAX = 12;
    const int RETRIES_PER_DIR = 8;
    const int DEEP_CHAIN_MAX_DEPTH = 22;

    // spends up to budget entries (dirs and files) under basePath, returns the dirs it made
    public static List<int> Grow(HomePlan plan, string basePath, int budget, HomeProfile profile, Random rng)
    {
        var start = plan.Entries;
        var root = plan.AddDir(basePath, profile.Kind);
        var members = new List<int> { root };
        AddFiles(plan, root, profile, rng, Math.Min(budget, plan.Remaining) - (plan.Entries - start));

        var failures = 0;
        while (plan.Entries - start < budget && plan.Remaining > 0 && failures < budget)
        {
            var parent = members[(int)(members.Count * Math.Pow(rng.NextDouble(), profile.Skew))];
            var chain = rng.NextDouble() < DEEP_CHAIN_CHANCE ? rng.Next(DEEP_CHAIN_MIN, DEEP_CHAIN_MAX + 1) : 1;
            var added = false;
            for (var step = 0; step < chain && plan.Remaining > 0; step++)
            {
                var child = TryAddChild(plan, parent, profile, rng, chain > 1 ? DEEP_CHAIN_MAX_DEPTH : profile.MaxDepth);
                if (child < 0)
                {
                    break;
                }
                added = true;
                members.Add(child);
                var allowed = Math.Min(budget - (plan.Entries - start), plan.Remaining);
                AddFiles(plan, child, profile, rng, allowed);
                parent = child;
            }
            failures = added ? 0 : failures + 1;
        }
        return members;
    }

    public static void AddFiles(HomePlan plan, int dir, HomeProfile profile, Random rng, int allowed)
    {
        var count = Math.Min(profile.SampleFileCount(rng), Math.Max(0, allowed));
        plan.AddGeneratedFiles(dir, count);
    }

    static int TryAddChild(HomePlan plan, int parent, HomeProfile profile, Random rng, int maxDepth)
    {
        if (plan.Depths[parent] >= maxDepth || plan.Paths[parent].Length > HomePlan.MAX_RELATIVE_PATH_CHARS)
        {
            return -1;
        }
        for (var attempt = 0; attempt < RETRIES_PER_DIR; attempt++)
        {
            var parentPath = plan.Paths[parent];
            var path = parentPath.Length == 0 ? profile.SampleDirName(rng) : $"{parentPath}/{profile.SampleDirName(rng)}";
            var index = plan.AddNewDir(path, profile.Kind);
            if (index >= 0)
            {
                return index;
            }
        }
        return -1;
    }
}
