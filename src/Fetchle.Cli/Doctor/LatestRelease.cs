using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fetchle.Cli.Doctor;

internal sealed record GitHubRelease(string? TagName);

[JsonSerializable(typeof(GitHubRelease))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal partial class GitHubJson : JsonSerializerContext;

// asks GitHub for the newest release tag. never throws: an unreachable network is a warning, not a crash
internal static class LatestRelease
{
    public const string API_VARIABLE = "FETCHLE_RELEASE_API";
    const string DEFAULT_API = "https://api.github.com/repos/codywilliamson/fetchle/releases/latest";
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public static async Task<(string? Tag, string? Error)> FetchAsync(Func<string, string?> getVariable)
    {
        var url = getVariable(API_VARIABLE) is { Length: > 0 } custom ? custom : DEFAULT_API;
        try
        {
            using var client = new HttpClient { Timeout = Timeout };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("fetchle-doctor");
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return (null, $"HTTP {(int)response.StatusCode}");
            }

            var release = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), GitHubJson.Default.GitHubRelease);
            return release?.TagName is { Length: > 0 } tag ? (tag, null) : (null, "no tag in the response");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return (null, e is TaskCanceledException ? "timed out" : "unreachable");
        }
    }
}
