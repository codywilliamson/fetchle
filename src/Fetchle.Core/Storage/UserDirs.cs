namespace Fetchle.Core.Storage;

// home and local app data from the environment first. on windows Environment.GetFolderPath goes through
// SHGetKnownFolderPath, which loads shell32 and cost several ms of every run's startup
public static class UserDirs
{
    public static string Home(Func<string, string?> getEnv) =>
        NonEmpty(getEnv(OperatingSystem.IsWindows() ? "USERPROFILE" : "HOME"))
        ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string LocalAppData(Func<string, string?> getEnv) =>
        NonEmpty(getEnv("LOCALAPPDATA")) ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
