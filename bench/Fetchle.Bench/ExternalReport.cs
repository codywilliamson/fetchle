using System.Text.Json.Serialization;

namespace Fetchle.Bench;

public sealed record ExternalResult(string Tool, string Shape, long P50Ms, long P95Ms, long[] RunsMs);

public sealed record ExternalReport(string Machine, string Os, int CorpusFiller, List<ExternalResult> Results);

[JsonSerializable(typeof(ExternalReport))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = true)]
partial class ExternalJson : JsonSerializerContext;
