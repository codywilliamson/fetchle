using System.Diagnostics;
using System.Text;

namespace Fetchle.E2E;

public sealed record CliRun(int ExitCode, string Stdout, byte[] StdoutBytes, string Stderr)
{
    public string[] Lines => Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

// runs the native exe with stdout redirected, so it never sees a tty
public static class Cli
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public static async Task<CliRun> RunAsync(string[] args, bool agent = false)
    {
        var psi = new ProcessStartInfo(NativeExe.Path, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // the suite may itself run under an agent, so set agent mode explicitly either way
        psi.Environment.Remove("CLAUDECODE");
        if (agent) psi.Environment["CLAUDECODE"] = "1";

        using var process = Process.Start(psi)!;
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
