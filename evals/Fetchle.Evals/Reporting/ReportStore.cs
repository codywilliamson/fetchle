using System.Text.Json;

namespace Fetchle.Evals.Reporting;

public sealed class ReportStore(string resultsPath, string baselinePath)
{
    public string ResultsPath => resultsPath;
    public string BaselinePath => baselinePath;

    public void WriteResults(EvalReport report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(resultsPath)!);
        File.WriteAllText(resultsPath, Serialize(report));
    }

    public void WriteBaseline(EvalReport report) => File.WriteAllText(baselinePath, Serialize(report) + "\n");

    public EvalReport ReadBaseline() =>
        JsonSerializer.Deserialize(File.ReadAllText(baselinePath), EvalJson.Default.EvalReport)!;

    static string Serialize(EvalReport report) => JsonSerializer.Serialize(report, EvalJson.Default.EvalReport);
}
