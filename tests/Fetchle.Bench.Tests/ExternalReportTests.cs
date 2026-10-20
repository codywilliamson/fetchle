using System.Text.Json;
using Fetchle.Bench;

namespace Fetchle.Bench.Tests;

public class ExternalReportTests
{
    static ExternalReport Sample() => new(
        "box", "Some OS 1.0", "linux", 8, "settings", 20,
        [new CorpusCounts("realistic", 100, 900, 1000, 90, 800, 890)],
        [new SkippedTool("fd", "not on PATH")],
        [
            new ExternalResult("rg", "realistic", ResultStatus.OK, null, "rg --files <corpus>", 65.5, 72.0, [65.5, 72.0]),
            new ExternalResult("cmd dir /s /b", "home-1m", ResultStatus.SKIPPED, "warmup took 70000 ms", "cmd /c dir", null, null, [70000]),
        ]);

    [Test]
    public async Task Json_is_snake_case_and_carries_the_machine_and_corpus_counts()
    {
        var json = JsonSerializer.Serialize(Sample(), ExternalJson.Default.ExternalReport);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        await Assert.That(root.GetProperty("cpu_count").GetInt32()).IsEqualTo(8);
        await Assert.That(root.GetProperty("os_family").GetString()).IsEqualTo("linux");
        await Assert.That(root.GetProperty("corpora")[0].GetProperty("walker_entries").GetInt32()).IsEqualTo(890);
        await Assert.That(root.GetProperty("skipped_tools")[0].GetProperty("tool").GetString()).IsEqualTo("fd");
        var first = root.GetProperty("results")[0];
        await Assert.That(first.GetProperty("p95_ms").GetDouble()).IsEqualTo(72.0);
        await Assert.That(first.GetProperty("runs_ms").GetArrayLength()).IsEqualTo(2);
    }

    [Test]
    public async Task A_skipped_result_keeps_its_reason_and_has_no_percentiles()
    {
        var json = JsonSerializer.Serialize(Sample(), ExternalJson.Default.ExternalReport);
        var roundTrip = JsonSerializer.Deserialize(json, ExternalJson.Default.ExternalReport)!;
        var skipped = roundTrip.Results[1];

        await Assert.That(skipped.Status).IsEqualTo("skipped");
        await Assert.That(skipped.Reason).IsEqualTo("warmup took 70000 ms");
        await Assert.That(skipped.P50Ms).IsNull();
    }

    [Test]
    public async Task Shapes_env_rejects_unknown_names_and_defaults_to_all()
    {
        await Assert.That(ExternalBenchOptions.ParseShapes(null)).IsEquivalentTo(BenchCorpus.ExternalShapes);
        await Assert.That(ExternalBenchOptions.ParseShapes("realistic, dir-heavy")).IsEquivalentTo(["realistic", "dir-heavy"]);
        await Assert.That(() => ExternalBenchOptions.ParseShapes("nope")).Throws<ArgumentException>();
    }

    [Test]
    public async Task Home_shape_is_external_only()
    {
        await Assert.That(BenchCorpus.ExternalShapes).Contains(BenchCorpus.HOME_1M);
        await Assert.That(BenchCorpus.Shapes).DoesNotContain(BenchCorpus.HOME_1M);
    }
}
