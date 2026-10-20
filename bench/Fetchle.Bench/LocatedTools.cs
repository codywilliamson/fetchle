using Microsoft.Extensions.Logging;

namespace Fetchle.Bench;

// which of the compared tools this machine has. a missing one is logged and left out of the run
public sealed record LocatedTools(string? Fetchle, string? Rg, string? Fd, string? PowerShell, string? Cmd, string? Find)
{
    public static LocatedTools Locate(ExternalBenchOptions options, List<SkippedTool> skipped, ILogger logger)
    {
        string? Need(string tool, string? path, string reason)
        {
            if (path is null)
            {
                skipped.Add(new SkippedTool(tool, reason));
                logger.SkippingTool(tool, reason);
            }
            return path;
        }

        var fetchle = File.Exists(options.FetchleExe) ? options.FetchleExe : null;
        var rg = ExecutableLocator.Find("rg", options.ToolsDirectory);
        var fd = ExecutableLocator.Find("fd", options.ToolsDirectory) ?? ExecutableLocator.Find("fdfind", options.ToolsDirectory);
        var windows = OperatingSystem.IsWindows();
        var shell = windows ? ExecutableLocator.Find("pwsh", options.ToolsDirectory) ?? ExecutableLocator.Find("powershell", options.ToolsDirectory) : null;
        var cmd = windows ? ExecutableLocator.Find("cmd", options.ToolsDirectory) : null;
        var find = windows ? null : ExecutableLocator.Find("find", options.ToolsDirectory);

        return new LocatedTools(
            Need("fetchle", fetchle, $"no exe at {options.FetchleExe}, run dotnet build.cs publish or set FETCHLE_EXE"),
            Need("rg", rg, $"not in {ExternalBenchOptions.TOOLS_VARIABLE} or on PATH"),
            Need("fd", fd, $"not in {ExternalBenchOptions.TOOLS_VARIABLE} or on PATH"),
            windows ? Need("Get-ChildItem", shell, "neither pwsh nor powershell on PATH") : null,
            windows ? Need("cmd dir", cmd, "cmd.exe not found") : null,
            windows ? null : Need("find", find, "not on PATH"));
    }
}
