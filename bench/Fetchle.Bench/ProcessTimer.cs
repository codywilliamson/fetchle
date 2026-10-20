using System.Diagnostics;

namespace Fetchle.Bench;

public sealed class ProcessTimer
{
    public double TimeRunMs(ToolCommand command, TimeSpan timeout)
    {
        command.BeforeEachRun?.Invoke();

        var startInfo = new ProcessStartInfo(command.Executable, command.Arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var (name, value) in command.Environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[name] = value;
        }

        var start = Stopwatch.GetTimestamp();
        using var process = Process.Start(startInfo)!;
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = Task.Run(() => process.StandardOutput.BaseStream.CopyTo(Stream.Null));
        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{command.CommandLine} still running after {timeout.TotalSeconds:0} s, killed it");
        }
        stdout.Wait();
        var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        // a failed run would otherwise look like a fast one
        if (process.ExitCode != command.ExpectedExitCode)
        {
            throw new InvalidOperationException(
                $"{command.CommandLine} exited {process.ExitCode}, expected {command.ExpectedExitCode}:\n{stderr.Result}");
        }
        return elapsedMs;
    }
}
