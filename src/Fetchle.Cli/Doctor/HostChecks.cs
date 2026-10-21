namespace Fetchle.Cli.Doctor;

internal static class HostChecks
{
    public const long MIN_INOTIFY_WATCHES = 65_536;
    const string NOT_YET = "not in v0.1";
    const string INOTIFY_FIX = "sudo sysctl fs.inotify.max_user_watches=524288";

    public static List<CheckResult> NotInThisVersion() =>
    [
        new("watcher", CheckStatus.Skip, NOT_YET),
        new("ntfs-turbo", CheckStatus.Skip, NOT_YET),
    ];

    // the model is optional: without it ranking is lexical only, so a missing one warns and a broken one fails
    public static CheckResult Model(string path, Func<string, Core.Embedding.StaticModel?> open)
    {
        try
        {
            using var model = open(path);
            return model is null
                ? new CheckResult("model", CheckStatus.Warn, $"no model at {path}, search ranks by name only", FixCommands.InstallScript)
                : new CheckResult("model", CheckStatus.Pass, $"{path}, checksum ok");
        }
        catch (Core.Embedding.ModelUnreadableException e)
        {
            return new CheckResult("model", CheckStatus.Fail, e.Message, FixCommands.InstallScript);
        }
    }

    // limit is null off Linux or when the proc file can't be read
    public static CheckResult Inotify(long? limit)
    {
        if (limit is null)
        {
            return new CheckResult("inotify", CheckStatus.Skip, "linux only");
        }

        if (limit < MIN_INOTIFY_WATCHES)
        {
            return new CheckResult("inotify", CheckStatus.Warn, $"max_user_watches is {limit:N0}, watching big trees needs at least {MIN_INOTIFY_WATCHES:N0}", INOTIFY_FIX);
        }

        return new CheckResult("inotify", CheckStatus.Pass, $"max_user_watches is {limit:N0}");
    }

    public static CheckResult Terminal(bool stdoutRedirected, Func<string, string?> getVariable)
    {
        var parts = new List<string> { stdoutRedirected ? "stdout redirected" : "stdout is a terminal" };
        if (!string.IsNullOrEmpty(getVariable("NO_COLOR")))
        {
            parts.Add("NO_COLOR set");
        }

        foreach (var name in new[] { "TERM", "COLORTERM" })
        {
            if (getVariable(name) is { Length: > 0 } value)
            {
                parts.Add($"{name}={value}");
            }
        }

        return new CheckResult("terminal", CheckStatus.Pass, string.Join(", ", parts));
    }

    public static long? ReadInotifyLimit()
    {
        const string PATH = "/proc/sys/fs/inotify/max_user_watches";
        if (!OperatingSystem.IsLinux() || !File.Exists(PATH))
        {
            return null;
        }

        try
        {
            return long.TryParse(File.ReadAllText(PATH).Trim(), out var limit) ? limit : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
