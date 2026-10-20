using Fetchle.Cli.Commands;
using Fetchle.Cli.Install;
using Fetchle.Core.Index;
using Fetchle.Core.Storage;

namespace Fetchle.Cli.Doctor;

// gathers every check in print order. the checks themselves are pure and live in their own files
internal static class DoctorChecks
{
    public static async Task<List<CheckResult>> GatherAsync(CliEnvironment env)
    {
        var dirs = env.Dirs;
        var entry = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dirs.Bin));
        var (receipt, receiptError) = LoadReceipt(dirs.Receipt);
        var (latest, latestError) = await LatestRelease.FetchAsync(env.GetVariable);

        var results = new List<CheckResult>
        {
            PathCheck.Evaluate(PersistedPathCount(env, receipt, entry), PathEntries.Count(env.GetVariable("PATH"), entry, PathRules.Current), entry),
            VersionCheck.Evaluate(env.Version, latest, latestError),
            ReceiptChecks.Receipt(receipt, receiptError, dirs.Receipt),
        };
        results.AddRange(IndexCheck.Evaluate(new IndexStore(dirs.Index).ReadInfo, RootsConfig.Load(dirs.Config), env.Clock.GetUtcNow()));
        results.AddRange(HostChecks.NotInThisVersion());
        results.Add(HostChecks.Inotify(HostChecks.ReadInotifyLimit()));
        results.Add(ReceiptChecks.Agents(receipt, File.Exists));
        results.Add(HostChecks.Terminal(env.StdoutRedirected, env.GetVariable));
        return results;
    }

    static (Receipt?, string?) LoadReceipt(string path)
    {
        try
        {
            return (ReceiptStore.Load(path), null);
        }
        catch (InvalidDataException e)
        {
            return (null, e.Message);
        }
    }

    // the receipt knows which rc file setup used, otherwise ask what setup would pick today
    static int PersistedPathCount(CliEnvironment env, Receipt? receipt, string entry)
    {
        var persistence = receipt?.PathEdit is { } edit ? PathPersistence.FromReceipt(edit) : null;
        return (persistence ?? PathPersistence.ForSetup(env)).Count(entry);
    }
}
