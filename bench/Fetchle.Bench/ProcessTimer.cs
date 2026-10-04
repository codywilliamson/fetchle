using System.Diagnostics;

namespace Fetchle.Bench;

public sealed class ProcessTimer
{
    public long TimeRunMs(ToolCommand command)
    {
        var startInfo = new ProcessStartInfo(command.Executable, command.Arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var start = Stopwatch.GetTimestamp();
        using var process = Process.Start(startInfo)!;
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardOutput.BaseStream.CopyTo(Stream.Null);
        process.WaitForExit();
        var elapsedMs = (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        // a failed run would otherwise look like a fast one
        if (process.ExitCode != command.ExpectedExitCode)
        {
            throw new InvalidOperationException(
                $"{command.CommandLine} exited {process.ExitCode}, expected {command.ExpectedExitCode}:\n{stderr.Result}");
        }
        return elapsedMs;
    }
}
