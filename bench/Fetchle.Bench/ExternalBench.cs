using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fetchle.Bench;

// times whole processes on the same corpus: the native fetchle exe vs rg --files.
// process vs process is the fair comparison; in-process numbers come from benchmarkdotnet
public static class ExternalBench
{
    const int Runs = 5;

    public static void Run(string repo, string resultsDir)
    {
        var fetchle = Environment.GetEnvironmentVariable("FETCHLE_EXE") is { Length: > 0 } exe
            ? exe
            : Path.Combine(repo, "artifacts", "publish", OperatingSystem.IsWindows() ? "fetchle.exe" : "fetchle");

        var results = new List<ExternalResult>();
        foreach (var shape in BenchCorpus.Shapes)
        {
            var root = BenchCorpus.Ensure(shape);
            if (File.Exists(fetchle))
                results.Add(Time("fetchle naive, full walk", shape, fetchle, ["zzz", "--root", root, "--budget", "5m", "--plain"]));
            else
                Console.WriteLine($"skipping fetchle: no exe at {fetchle}, run ./build.ps1 publish or set FETCHLE_EXE");

            if (OnPath("rg") is { } rg)
            {
                results.Add(Time("rg --files", shape, rg, ["--files", "--hidden", "--no-ignore", root]));
                results.Add(Time("rg -j1 --files", shape, rg, ["-j1", "--files", "--hidden", "--no-ignore", root]));
            }
            else
            {
                Console.WriteLine("skipping rg: not on PATH");
            }
        }

        foreach (var r in results) Console.WriteLine($"{r.Shape,-10} {r.Tool,-26} median {r.MedianMs,6} ms  runs {string.Join(", ", r.RunsMs)}");
        var path = Path.Combine(resultsDir, "external.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new ExternalReport(Environment.MachineName, RuntimeInfo(), BenchCorpus.Filler, results), ExternalJson.Default.ExternalReport) + "\n");
        Console.WriteLine($"wrote {path}");
    }

    static ExternalResult Time(string tool, string shape, string exe, string[] args)
    {
        var runs = new long[Runs];
        // one untimed warmup so every tool sees a warm cache
        RunOnce(exe, args);
        for (var i = 0; i < Runs; i++) runs[i] = RunOnce(exe, args);
        var sorted = (long[])runs.Clone();
        Array.Sort(sorted);
        return new ExternalResult(tool, shape, sorted[Runs / 2], runs);
    }

    static long RunOnce(string exe, string[] args)
    {
        var psi = new ProcessStartInfo(exe, args) { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.Environment.Remove("CLAUDECODE");
        var start = Stopwatch.GetTimestamp();
        using var p = Process.Start(psi)!;
        p.StandardOutput.BaseStream.CopyTo(Stream.Null);
        p.WaitForExit();
        return (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    static string? OnPath(string name)
    {
        var exe = OperatingSystem.IsWindows() ? name + ".exe" : name;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(dir, exe);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    static string RuntimeInfo() => $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription}, {Environment.ProcessorCount} cpus";
}

sealed record ExternalResult(string Tool, string Shape, long MedianMs, long[] RunsMs);

sealed record ExternalReport(string Machine, string Os, int CorpusFiller, List<ExternalResult> Results);

[JsonSerializable(typeof(ExternalReport))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = true)]
partial class ExternalJson : JsonSerializerContext;
