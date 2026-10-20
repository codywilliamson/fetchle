using System.Text.Json;

namespace Fetchle.E2E.Harness;

// a throwaway home, data dir and install dir, wired in through env vars so a run never touches the real install.
// on Windows the user PATH lives in the registry, which HOME can't redirect, so tests that edit it only run on CI
public sealed class InstallSandbox : IDisposable
{
    public const string ALLOW_REGISTRY_VARIABLE = "FETCHLE_E2E_ALLOW_REGISTRY";
    public const string OFFLINE_RELEASE_API = "http://127.0.0.1:1/";

    public string Home { get; } = Path.Combine(Path.GetTempPath(), $"fetchle-e2e-home-{Guid.NewGuid():N}");

    public string Data => Path.Combine(Home, ".local", "share", "fetchle");

    public string Bin => Path.Combine(Data, "bin");

    public string Receipt => Path.Combine(Data, "receipt.json");

    public string RcFile => Path.Combine(Home, ".bashrc");

    public static string ExeName => OperatingSystem.IsWindows() ? "fetchle.exe" : "fetchle";

    public static bool CanEditUserPath =>
        !OperatingSystem.IsWindows()
        || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true"
        || Environment.GetEnvironmentVariable(ALLOW_REGISTRY_VARIABLE) == "1";

    public InstallSandbox()
    {
        Directory.CreateDirectory(Home);
    }

    public IReadOnlyDictionary<string, string> Env => new Dictionary<string, string>
    {
        ["HOME"] = Home,
        ["SHELL"] = "/bin/bash",
        ["FETCHLE_DATA_DIR"] = Data,
        ["FETCHLE_INSTALL_DIR"] = Bin,
        ["FETCHLE_RELEASE_API"] = OFFLINE_RELEASE_API,
    };

    // what an install leaves behind: the exe in the install dir
    public string InstallExe()
    {
        Directory.CreateDirectory(Bin);
        var exe = Path.Combine(Bin, ExeName);
        File.Copy(NativeExe.Path, exe, overwrite: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return exe;
    }

    public Task<CliRun> RunAsync(string exe, params string[] args) =>
        FetchleProcess.RunAsync(args, dataDir: Data, environment: Env, exe: exe);

    public void WriteReceipt(string channel, bool withPathEdit = false)
    {
        Directory.CreateDirectory(Data);
        var edit = withPathEdit
            ? $$""", "path_edit": { "kind": "shell-rc-block", "entry": {{JsonSerializer.Serialize(Bin)}}, "file": {{JsonSerializer.Serialize(RcFile)}} }"""
            : "";
        File.WriteAllText(Receipt, $$"""{ "version": "0.0.0", "channel": "{{channel}}", "install_dir": {{JsonSerializer.Serialize(Bin)}}, "data_dir": {{JsonSerializer.Serialize(Data)}}{{edit}}, "agents": [] }""");
    }

    public string[] AllFiles() => Directory.GetFiles(Home, "*", SearchOption.AllDirectories);

    public async Task<bool> WaitUntilGoneAsync(string dir, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (Directory.Exists(dir) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(250);
        }

        return !Directory.Exists(dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Home, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a background cleanup may still be holding it
        }
    }
}
