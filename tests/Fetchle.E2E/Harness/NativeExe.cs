using System.Diagnostics;

namespace Fetchle.E2E.Harness;

// the published native exe is the product, so e2e drives it, docs/testing.md.
// published once per run; set FETCHLE_EXE to reuse an existing build
public static class NativeExe
{
    public static string Path { get; private set; } = "";

    [Before(TestSession)]
    public static async Task Publish()
    {
        if (Environment.GetEnvironmentVariable("FETCHLE_EXE") is { Length: > 0 } existing)
        {
            Path = System.IO.Path.GetFullPath(existing);
            return;
        }

        var repo = RepoRoot();
        var output = System.IO.Path.Combine(repo, "artifacts", "e2e");
        var psi = new ProcessStartInfo("dotnet", ["publish", System.IO.Path.Combine(repo, "src", "Fetchle.Cli"), "-c", "Release", "-o", output])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // aot link needs vswhere.exe on PATH, see docs/release.md
        var vsInstaller = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer");
        if (OperatingSystem.IsWindows() && Directory.Exists(vsInstaller))
            psi.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH") + System.IO.Path.PathSeparator + vsInstaller;

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"native publish failed ({process.ExitCode}):\n{await stdout}\n{await stderr}");

        Path = System.IO.Path.Combine(output, OperatingSystem.IsWindows() ? "fetchle.exe" : "fetchle");
    }

    static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "fetchle.slnx"))) return dir.FullName;
        throw new InvalidOperationException("can't find fetchle.slnx above " + AppContext.BaseDirectory);
    }
}
