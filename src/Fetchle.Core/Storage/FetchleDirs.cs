namespace Fetchle.Core.Storage;

// where fetchle keeps its binary and its data, per docs/specs/install.md
public sealed record FetchleDirs(string Data, string Bin)
{
    // tests and the bench point these at a temp dir so they never touch the real install
    public const string DATA_DIR_VARIABLE = "FETCHLE_DATA_DIR";
    public const string INSTALL_DIR_VARIABLE = "FETCHLE_INSTALL_DIR";

    public string Index => Path.Combine(Data, "index");

    public string Config => Path.Combine(Data, "config.json");

    public string Receipt => Path.Combine(Data, "receipt.json");

    public static FetchleDirs Resolve(Func<string, string?> getEnv)
    {
        var home = NonEmpty(getEnv("HOME")) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string data;
        string bin;
        if (OperatingSystem.IsWindows())
        {
            // the env var, not the known-folder api, so a test can redirect it like the install script does
            var local = NonEmpty(getEnv("LOCALAPPDATA")) ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            data = Path.Combine(local, "fetchle");
            bin = Path.Combine(data, "bin");
        }
        else if (OperatingSystem.IsMacOS())
        {
            data = Path.Combine(home, "Library", "Application Support", "fetchle");
            bin = Path.Combine(home, ".local", "share", "fetchle", "bin");
        }
        else
        {
            var xdg = NonEmpty(getEnv("XDG_DATA_HOME")) ?? Path.Combine(home, ".local", "share");
            data = Path.Combine(xdg, "fetchle");
            bin = Path.Combine(home, ".local", "share", "fetchle", "bin");
        }

        return new FetchleDirs(NonEmpty(getEnv(DATA_DIR_VARIABLE)) ?? data, NonEmpty(getEnv(INSTALL_DIR_VARIABLE)) ?? bin);
    }

    static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
