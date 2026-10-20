using Fetchle.Core.Index;
using Fetchle.Core.Storage;

namespace Fetchle.Bench;

// isolated fetchle data dirs for the indexed commands, so the bench never touches the real install
public sealed record IndexDirs(string Query, string Rebuild);

// every command the external bench times, built from nothing but the located tools and the corpus root
public static class ToolPlans
{
    public const string QUERY = "settings";
    public const string NO_MATCH_QUERY = "zzzz-no-match";

    const string BUDGET = "5m";
    const int SUCCESS = 0;
    const int NO_MATCH = 1;

    public static List<ToolPlan> For(LocatedTools tools, string root, IndexDirs indexDirs)
    {
        var plans = new List<ToolPlan>();
        if (tools.Fetchle is { } fetchle)
        {
            plans.AddRange(FetchlePlans(fetchle, root, indexDirs));
        }
        if (tools.Rg is { } rg)
        {
            // both walk everything, hidden and ignored included, and print paths whose name matches, case-insensitive
            string[] files = ["--files", "--hidden", "--no-ignore", "--iglob", $"*{QUERY}*", root];
            plans.Add(Plain(new ToolCommand("rg --files --iglob", rg, files, SUCCESS)));
            plans.Add(Plain(new ToolCommand("rg -j1 --files --iglob", rg, ["-j1", .. files], SUCCESS)));
        }
        if (tools.Fd is { } fd)
        {
            // -H hidden, -I no ignore files, -i case-insensitive. the pattern is a regex on the file name only
            plans.Add(Plain(new ToolCommand("fd -H -I -i", fd, ["-H", "-I", "-i", "--color", "never", QUERY, root], SUCCESS)));
        }
        if (tools.PowerShell is { } shell)
        {
            var script = $"Get-ChildItem -LiteralPath '{root.Replace("'", "''")}' -Recurse -Force -Filter *{QUERY}* | ForEach-Object FullName";
            plans.Add(Plain(new ToolCommand("Get-ChildItem -Recurse", shell, ["-NoProfile", "-NonInteractive", "-Command", script], SUCCESS)));
        }
        if (tools.Cmd is { } cmd)
        {
            plans.Add(Plain(new ToolCommand("cmd dir /s /b", cmd, ["/c", "dir", "/s", "/b", Path.Combine(root, $"*{QUERY}*")], SUCCESS)));
        }
        if (tools.Find is { } find)
        {
            plans.Add(Plain(new ToolCommand("find -iname", find, [root, "-iname", $"*{QUERY}*"], SUCCESS)));
        }
        return plans;
    }

    static List<ToolPlan> FetchlePlans(string fetchle, string root, IndexDirs indexDirs)
    {
        string[] liveWalk = ["--root", root, "--budget", BUDGET, "--plain"];
        var queryEnv = DataDirEnvironment(indexDirs.Query);
        var rebuildEnv = DataDirEnvironment(indexDirs.Rebuild);
        var rebuildSnapshot = new IndexStore(new FetchleDirs(indexDirs.Rebuild, indexDirs.Rebuild).Index).SnapshotPath;

        return
        [
            Plain(new ToolCommand("fetchle live walk", fetchle, [QUERY, .. liveWalk], SUCCESS)),

            // the walk the rebuild is compared to: same roots, same prune rules, nothing matches so nothing is ranked or printed
            Plain(new ToolCommand("fetchle bare walk", fetchle, [NO_MATCH_QUERY, .. liveWalk], NO_MATCH)),

            // no --root, so it reads the configured roots through the snapshot
            new ToolPlan(
                new ToolCommand("fetchle indexed query", fetchle, [QUERY, "--budget", BUDGET, "--plain"], SUCCESS, queryEnv),
                [
                    new ToolCommand("fetchle roots add", fetchle, ["roots", "add", root], SUCCESS, queryEnv),
                    new ToolCommand("fetchle index rebuild", fetchle, ["index", "rebuild"], SUCCESS, queryEnv),
                ]),

            // the snapshot goes before every run so each rebuild starts cold
            new ToolPlan(
                new ToolCommand("fetchle cold rebuild", fetchle, ["index", "rebuild"], SUCCESS, rebuildEnv, () => DeleteIfThere(rebuildSnapshot)),
                [new ToolCommand("fetchle roots add", fetchle, ["roots", "add", root], SUCCESS, rebuildEnv)]),
        ];
    }

    static Dictionary<string, string> DataDirEnvironment(string dataDir) => new() { [FetchleDirs.DATA_DIR_VARIABLE] = dataDir };

    // the first run has no snapshot, and maybe no index dir yet
    static void DeleteIfThere(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    static ToolPlan Plain(ToolCommand command) => new(command, []);
}
