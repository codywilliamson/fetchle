using Fetchle.Core.Storage;
using Fetchle.Core.Walking;

namespace Fetchle.Cli.Install;

// what uninstall would do, decided from the receipt alone
internal sealed record UninstallPlan(Receipt Receipt, List<string> Steps, string? Problem)
{
    public static UninstallPlan From(Receipt receipt, string home)
    {
        var steps = new List<string>();
        if (receipt.PathEdit is { } edit)
        {
            steps.Add(edit.File is null ? $"remove {edit.Entry} from the user PATH" : $"remove the fetchle block from {edit.File}");
        }

        steps.Add($"delete fetchle's files in {receipt.InstallDir}, then the dir if that empties it");
        steps.Add($"delete fetchle's files in {receipt.DataDir}, then the dir if that empties it");
        return new UninstallPlan(receipt, steps, FindUnsafe([receipt.InstallDir, receipt.DataDir], home));
    }

    // removal is file by file and never recursive, so this is a second fence: a corrupt receipt naming a root,
    // home or anything above home is refused outright
    static string? FindUnsafe(IEnumerable<string> dirs, string home)
    {
        var fullHome = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        foreach (var dir in dirs)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Path.IsPathFullyQualified(dir))
            {
                return $"'{dir}' is not an absolute path";
            }

            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir));
            var root = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full) ?? "");
            if (full.Equals(root, PathComparison.Default) || RootsConfig.Covers(full, fullHome))
            {
                return $"'{dir}' is a root, the home directory or above it";
            }
        }

        return null;
    }
}

// what to tell someone whose install came from a package manager
internal static class ChannelCommands
{
    public static string? UninstallCommand(string channel) => channel switch
    {
        "brew" => "brew uninstall fetchle",
        "winget" => "winget uninstall fetchle",
        "scoop" => "scoop uninstall fetchle",
        "dotnet-tool" => "dotnet tool uninstall -g fetchle",
        _ => null,
    };
}
