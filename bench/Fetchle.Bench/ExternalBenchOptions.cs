namespace Fetchle.Bench;

public sealed record ExternalBenchOptions(string FetchleExe, string ResultsDirectory)
{
    const string FETCHLE_EXE_VARIABLE = "FETCHLE_EXE";

    public static ExternalBenchOptions FromEnvironment(string repo, string resultsDirectory)
    {
        var fetchleExe = Environment.GetEnvironmentVariable(FETCHLE_EXE_VARIABLE);
        if (string.IsNullOrEmpty(fetchleExe))
        {
            var exeName = OperatingSystem.IsWindows() ? "fetchle.exe" : "fetchle";
            fetchleExe = Path.Combine(repo, "artifacts", "publish", exeName);
        }
        return new ExternalBenchOptions(fetchleExe, resultsDirectory);
    }
}
