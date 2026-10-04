using System.Text.Json;
using System.Text.Json.Serialization;
using Fetchle.Core.Search;

namespace Fetchle.Mcp;

sealed record FindFilesResult(IReadOnlyList<SearchHit> Paths, int TotalMatches, int Shown, long ElapsedMs, string? StoppedEarly);

[JsonSerializable(typeof(FindFilesArgs))]
[JsonSerializable(typeof(IndexStatusArgs))]
[JsonSerializable(typeof(IDictionary<string, JsonElement>))]
[JsonSerializable(typeof(FindFilesResult))]
[JsonSerializable(typeof(IndexStatus))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true)]
partial class McpJson : JsonSerializerContext;
