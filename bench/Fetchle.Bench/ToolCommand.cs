namespace Fetchle.Bench;

// BeforeEachRun runs untimed, right before every run of the command (warmup included)
public sealed record ToolCommand(
    string Tool,
    string Executable,
    string[] Arguments,
    int ExpectedExitCode,
    IReadOnlyDictionary<string, string>? Environment = null,
    Action? BeforeEachRun = null)
{
    public string CommandLine => $"{Executable} {string.Join(' ', Arguments)}";
}
