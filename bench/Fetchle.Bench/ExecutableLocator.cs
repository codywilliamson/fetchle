namespace Fetchle.Bench;

public static class ExecutableLocator
{
    public static string? FindOnPath(string name)
    {
        var exeName = OperatingSystem.IsWindows() ? name + ".exe" : name;
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var directory in path.Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(directory, exeName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }
}
