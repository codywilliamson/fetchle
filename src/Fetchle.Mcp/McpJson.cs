using System.Text.Json.Serialization;
using Fetchle.Core;

namespace Fetchle.Mcp;

// structuredContent of find_files, docs/specs/mcp.md
sealed record FindFilesResult(IReadOnlyList<SearchHit> Paths, int TotalMatches, int Shown, long ElapsedMs, string? StoppedEarly);

[JsonSerializable(typeof(FindFilesResult))]
[JsonSerializable(typeof(IndexStatus))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
partial class McpJson : JsonSerializerContext;
