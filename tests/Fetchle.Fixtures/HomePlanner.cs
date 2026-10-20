namespace Fetchle.Fixtures;

// lays out a fake home dir: repos with node_modules and .git, app data, caches, documents, package stores.
// clusters are picked by whichever kind is furthest under its share of the target, so the mix holds
// whatever sizes the rng draws
static class HomePlanner
{
    const int MIN_CLUSTER_ENTRIES = 300;
    const string REPOS_ROOT = "source/repos";
    const int REPO_DEPTH = 3;
    const int NESTED_PROJECTS_PER_REPO = 2;

    enum Cluster { Repo, NodeModules, Git, Cache, AppData, Media, Packages }

    // share of the target, then the log-uniform size range of one cluster
    static readonly (Cluster Kind, double Share, int MinSize, int MaxSize)[] Mix =
    [
        (Cluster.Repo, 0.16, 1_500, 25_000),
        (Cluster.NodeModules, 0.20, 1_000, 70_000),
        (Cluster.Git, 0.08, 800, 20_000),
        (Cluster.Cache, 0.07, 1_000, 30_000),
        (Cluster.AppData, 0.25, 600, 25_000),
        (Cluster.Media, 0.09, 300, 8_000),
        (Cluster.Packages, 0.15, 1_000, 30_000),
    ];

    static readonly string[] AppDataRoots = ["Local", "Local", "Roaming", "Roaming", "LocalLow"];

    public static HomePlan Build(int target, int seed)
    {
        var rng = new Random(seed);
        var plan = new HomePlan(target);
        var projects = new List<string> { $"{REPOS_ROOT}/fetchle" };
        var reposWithoutGit = new List<string> { $"{REPOS_ROOT}/fetchle" };

        foreach (var dotfile in HomeWords.HomeDotfiles)
        {
            plan.AddFixedFile(0, dotfile);
        }
        foreach (var landmark in Corpus.Landmarks)
        {
            var slash = landmark.LastIndexOf('/');
            plan.AddFixedFile(plan.AddDir(landmark[..slash], FileKind.Data), landmark[(slash + 1)..]);
        }

        var spent = new double[Mix.Length];
        var lastDir = 0;
        while (plan.Remaining >= MIN_CLUSTER_ENTRIES)
        {
            var slot = Neediest(spent, target);
            var (kind, _, minSize, maxSize) = Mix[slot];
            if (kind == Cluster.Git && reposWithoutGit.Count == 0)
            {
                kind = Cluster.Repo;
            }

            var before = plan.Entries;
            var size = Math.Min((int)(minSize * Math.Pow((double)maxSize / minSize, rng.NextDouble())), plan.Remaining);
            lastDir = Run(kind, plan, size, rng, projects, reposWithoutGit);
            spent[slot] += plan.Entries - before;
        }

        // too little left for another cluster, so top up the last dir to land exactly on the target
        plan.AddGeneratedFiles(lastDir, plan.Remaining);
        return plan;
    }

    static int Neediest(double[] spent, int target)
    {
        var best = 0;
        for (var i = 1; i < Mix.Length; i++)
        {
            if (Mix[i].Share * target - spent[i] > Mix[best].Share * target - spent[best])
            {
                best = i;
            }
        }
        return best;
    }

    // returns a dir of the new cluster for the final top-up
    static int Run(Cluster kind, HomePlan plan, int size, Random rng, List<string> projects, List<string> reposWithoutGit)
    {
        switch (kind)
        {
            case Cluster.Repo:
                return NewRepo(plan, size, rng, projects, reposWithoutGit);
            case Cluster.NodeModules:
                return TreeGrower.Grow(plan, $"{projects[rng.Next(projects.Count)]}/node_modules", size, HomeProfile.Npm, rng)[0];
            case Cluster.Git:
                var repo = reposWithoutGit[rng.Next(reposWithoutGit.Count)];
                reposWithoutGit.Remove(repo);
                GitGrower.Grow(plan, $"{repo}/.git", size, rng);
                return plan.AddDir($"{repo}/.git", FileKind.Data);
            case Cluster.Cache:
                return TreeGrower.Grow(plan, $".cache/{HomeWords.CacheApps[rng.Next(HomeWords.CacheApps.Length)]}", size, HomeProfile.Cache, rng)[0];
            case Cluster.AppData:
                var appRoot = AppDataRoots[rng.Next(AppDataRoots.Length)];
                var app = $"AppData/{appRoot}/{HomeWords.Vendors[rng.Next(HomeWords.Vendors.Length)]}/{HomeWords.Products[rng.Next(HomeWords.Products.Length)]}";
                return TreeGrower.Grow(plan, app, size, HomeProfile.AppData, rng)[0];
            case Cluster.Media:
                var mediaRoot = HomeWords.MediaRoots[rng.Next(HomeWords.MediaRoots.Length)];
                var profile = mediaRoot == "Documents" ? HomeProfile.Documents : HomeProfile.Media;
                return TreeGrower.Grow(plan, $"{mediaRoot}/{HomeWords.MediaDirs[rng.Next(HomeWords.MediaDirs.Length)]}", size, profile, rng)[0];
            default:
                return TreeGrower.Grow(plan, HomeWords.PackageRoots[rng.Next(HomeWords.PackageRoots.Length)], size, HomeProfile.Packages, rng)[0];
        }
    }

    static int NewRepo(HomePlan plan, int size, Random rng, List<string> projects, List<string> reposWithoutGit)
    {
        var basePath = $"{REPOS_ROOT}/{HomeWords.RepoNames[rng.Next(HomeWords.RepoNames.Length)]}-{rng.Next(1000)}";
        var members = TreeGrower.Grow(plan, basePath, size, HomeProfile.Code, rng);
        projects.Add(basePath);
        reposWithoutGit.Add(basePath);

        // a few shallow subdirs stand in for monorepo packages that carry their own node_modules
        var nested = members.Where(m => plan.Depths[m] <= REPO_DEPTH + 2 && plan.Depths[m] > REPO_DEPTH).Take(NESTED_PROJECTS_PER_REPO);
        projects.AddRange(nested.Select(m => plan.Paths[m]));
        return members[0];
    }
}
