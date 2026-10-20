namespace Fetchle.Cli.Doctor;

internal static class VersionCheck
{
    const string NAME = "version";

    // latest is a release tag like v0.2.0, or null with error saying why we couldn't ask
    public static CheckResult Evaluate(string current, string? latest, string? error)
    {
        if (latest is null)
        {
            return new CheckResult(NAME, CheckStatus.Warn, $"{current}, can't check for a newer release ({error})");
        }

        var currentVersion = Parse(current);
        var latestVersion = Parse(latest);
        if (currentVersion is null || latestVersion is null)
        {
            return new CheckResult(NAME, CheckStatus.Warn, $"{current}, latest is {latest}, can't compare them");
        }

        if (currentVersion < latestVersion)
        {
            return new CheckResult(NAME, CheckStatus.Warn, $"{current}, latest is {latest}", FixCommands.InstallScript);
        }

        return new CheckResult(NAME, CheckStatus.Pass, $"{current} is the latest release");
    }

    // "v0.2.0-rc1+abc" -> 0.2.0
    static Version? Parse(string text)
    {
        var core = text.TrimStart('v');
        var cut = core.IndexOfAny(['-', '+']);
        return Version.TryParse(cut < 0 ? core : core[..cut], out var version) ? version : null;
    }
}
