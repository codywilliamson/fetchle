using System.Diagnostics;
using System.Text;

namespace Fetchle.E2E.Harness;

public sealed record CliRun(int ExitCode, string Stdout, byte[] StdoutBytes, string Stderr)
{
    public string[] Lines => Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public static class FetchleProcess
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // every run gets a data dir of its own unless the test passes one, so nothing touches the real install.
    // environment overrides win over everything else; exe runs a copy instead of the shared build
    public static async Task<CliRun> RunAsync(string[] args, bool agent = false, string? dataDir = null, IReadOnlyDictionary<string, string>? environment = null, string? exe = null)
    {
        var psi = new ProcessStartInfo(exe ?? NativeExe.Path, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // the suite may itself run under an agent, so set agent mode explicitly either way
        psi.Environment.Remove("CLAUDECODE");
        if (agent)
        {
            psi.Environment["CLAUDECODE"] = "1";
        }

        psi.Environment["FETCHLE_DATA_DIR"] = dataDir ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fetchle-e2e-data-{Guid.NewGuid():N}");
        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            psi.Environment[name] = value;
        }

        using var process = Process.Start(psi)!;
        // nothing here answers prompts
        process.StandardInput.Close();
        using var stdout = new MemoryStream();
        var copy = process.StandardOutput.BaseStream.CopyToAsync(stdout);
        var stderr = process.StandardError.ReadToEndAsync();
        using var cts = new CancellationTokenSource(Timeout);
        await process.WaitForExitAsync(cts.Token);
        await copy;
        var bytes = stdout.ToArray();
        return new CliRun(process.ExitCode, Encoding.UTF8.GetString(bytes), bytes, await stderr);
    }
}
