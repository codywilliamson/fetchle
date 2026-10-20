namespace Fetchle.Bench;

public sealed record ExternalBenchOptions(string FetchleExe, string ResultsDirectory, string? ToolsDirectory, string[] Shapes)
{
    const string FETCHLE_EXE_VARIABLE = "FETCHLE_EXE";
    public const string TOOLS_VARIABLE = "FETCHLE_BENCH_TOOLS";
    const string SHAPES_VARIABLE = "FETCHLE_BENCH_SHAPES";

    public static ExternalBenchOptions FromEnvironment(string repo, string resultsDirectory)
    {
        var fetchleExe = Environment.GetEnvironmentVariable(FETCHLE_EXE_VARIABLE);
        if (string.IsNullOrEmpty(fetchleExe))
        {
            var exeName = OperatingSystem.IsWindows() ? "fetchle.exe" : "fetchle";
            fetchleExe = Path.Combine(repo, "artifacts", "publish", exeName);
        }
        var tools = Environment.GetEnvironmentVariable(TOOLS_VARIABLE);
        return new ExternalBenchOptions(fetchleExe, resultsDirectory, string.IsNullOrEmpty(tools) ? null : tools, ParseShapes(Environment.GetEnvironmentVariable(SHAPES_VARIABLE)));
    }

    // a comma list narrows the run to some shapes, for local checks. unset means all of them
    public static string[] ParseShapes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return BenchCorpus.ExternalShapes;
        }
        var shapes = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var unknown = shapes.Except(BenchCorpus.ExternalShapes).ToArray();
        if (unknown.Length > 0)
        {
            throw new ArgumentException($"unknown shape(s) in {SHAPES_VARIABLE}: {string.Join(", ", unknown)}. known: {string.Join(", ", BenchCorpus.ExternalShapes)}");
        }
        return shapes;
    }

    public static string OsFamily => OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";

    // one results file per os, since each os runs on its own self-hosted runner
    public static string ResultsFileName => $"external-{OsFamily}.json";
}
