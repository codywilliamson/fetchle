using System.Text.Json;
using System.Text.Json.Serialization;
using Fetchle.Core.Search;

namespace Fetchle.Cli.Output;

// json lines: one hit per line, then a summary line
static class JsonOutput
{
    public static void Write(TextWriter output, SearchResult result)
    {
        foreach (var hit in result.Hits)
            output.WriteLine(JsonSerializer.Serialize(hit, CliJson.Default.SearchHit));
        var summary = new JsonSummary(result.TotalMatches, result.Hits.Count, (long)result.Elapsed.TotalMilliseconds, result.StoppedEarly);
        output.WriteLine(JsonSerializer.Serialize(summary, CliJson.Default.JsonSummary));
    }
}

sealed record JsonSummary(int Total, int Shown, long ElapsedMs, string? StoppedEarly);

[JsonSerializable(typeof(SearchHit))]
[JsonSerializable(typeof(JsonSummary))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
partial class CliJson : JsonSerializerContext;
