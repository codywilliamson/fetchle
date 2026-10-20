namespace Fetchle.Bench;

public static class ExecutableLocator
{
    // the tools dir wins over PATH, so a pinned rg or fd beats whatever the runner image ships
    public static string? Find(string name, string? toolsDirectory)
    {
        var exeName = OperatingSystem.IsWindows() ? name + ".exe" : name;
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var directories = path.Split(Path.PathSeparator).Prepend(toolsDirectory ?? "");
        foreach (var directory in directories)
        {
            if (directory.Length == 0)
            {
                continue;
            }
            var candidate = Path.Combine(directory, exeName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }
}
