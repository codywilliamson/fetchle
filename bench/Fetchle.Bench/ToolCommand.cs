namespace Fetchle.Bench;

public sealed record ToolCommand(string Tool, string Executable, string[] Arguments, int ExpectedExitCode)
{
    public string CommandLine => $"{Executable} {string.Join(' ', Arguments)}";
}
