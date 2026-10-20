using Fetchle.Core.Storage;
using Fetchle.Cli.Output;
using Fetchle.Core.Index;
using Fetchle.Core.Walking;

namespace Fetchle.Cli.Doctor;

internal static class IndexCheck
{
    const string INDEX_NAME = "index";
    const string ROOTS_NAME = "roots";
    const string ADD_ROOT_FIX = "fetchle roots add <path>";

    // read is IndexStore.ReadInfo in production: null when there's no snapshot, IndexUnreadableException when it can't be trusted
    public static List<CheckResult> Evaluate(Func<SnapshotInfo?> read, IReadOnlyList<string> roots, DateTimeOffset now)
    {
        SnapshotInfo? info;
        try
        {
            info = read();
        }
        catch (IndexUnreadableException e)
        {
            var fix = roots.Count == 0 ? ADD_ROOT_FIX : FixCommands.INDEX_REBUILD;
            return [new CheckResult(INDEX_NAME, CheckStatus.Fail, e.Message, fix), new CheckResult(ROOTS_NAME, CheckStatus.Skip, "index unreadable")];
        }

        if (info is null)
        {
            return NoIndex(roots);
        }

        var detail = $"built {Humanize.Age(now - info.BuiltAt)}, {info.Entries:N0} entries, {Humanize.Size(info.Bytes)}, {info.BytesPerEntry:0.0} bytes per entry";
        return [new CheckResult(INDEX_NAME, CheckStatus.Pass, detail), RootHealth(info, roots)];
    }

    static List<CheckResult> NoIndex(IReadOnlyList<string> roots)
    {
        if (roots.Count == 0)
        {
            return [new CheckResult(INDEX_NAME, CheckStatus.Warn, "no roots configured, searches walk the current directory live", ADD_ROOT_FIX), new CheckResult(ROOTS_NAME, CheckStatus.Skip, "no roots")];
        }

        return [new CheckResult(INDEX_NAME, CheckStatus.Fail, "no index built yet", FixCommands.INDEX_REBUILD), new CheckResult(ROOTS_NAME, CheckStatus.Skip, "no index")];
    }

    static CheckResult RootHealth(SnapshotInfo info, IReadOnlyList<string> roots)
    {
        var problems = new List<string>();
        string? fix = null;
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                problems.Add($"{root} does not exist");
                fix ??= $"fetchle roots remove {root}";
            }
            // a root under an indexed one is answered from that snapshot, same as search does
            else if (!info.Roots.Any(r => RootsConfig.Covers(r.Path, root)))
            {
                problems.Add($"{root} is not in the index");
                fix ??= FixCommands.INDEX_REBUILD;
            }
        }

        if (problems.Count > 0)
        {
            return new CheckResult(ROOTS_NAME, CheckStatus.Fail, string.Join("; ", problems), fix);
        }

        return new CheckResult(ROOTS_NAME, CheckStatus.Pass, roots.Count == 0 ? "no roots configured" : $"{roots.Count} root(s) indexed");
    }
}
