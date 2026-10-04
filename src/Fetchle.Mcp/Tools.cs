using System.Text;
using System.Text.Json;
using Fetchle.Core.Search;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Fetchle.Mcp;

// find_files and index_status, docs/specs/mcp.md
public sealed class Tools(IFileSearch search, string defaultRoot)
{
    public const int MaxBudgetMs = 25_000;

    const string FindFilesSchema = """
        {
          "type": "object",
          "properties": {
            "query": { "type": "string", "description": "Plain-words description or partial name." },
            "limit": { "type": "integer", "minimum": 1, "default": 10, "description": "Max results." },
            "budget_ms": { "type": "integer", "minimum": 0, "maximum": 25000, "default": 2000, "description": "Hard time cap in ms." },
            "root": { "type": "string", "description": "Restrict to one root." },
            "ext": { "type": "array", "items": { "type": "string" }, "description": "Extension filter." }
          },
          "required": ["query"]
        }
        """;

    const string IndexStatusSchema = """{ "type": "object", "properties": {} }""";

    public static readonly List<Tool> Definitions =
    [
        new()
        {
            Name = "find_files",
            Description = "Ranked file and directory path search. Returns paths plus a footer with match count, time and whether the budget cut it short.",
            InputSchema = JsonDocument.Parse(FindFilesSchema).RootElement,
        },
        new()
        {
            Name = "index_status",
            Description = "Roots, file counts, last full scan, vector and watcher state. Use it to decide whether to trust a miss.",
            InputSchema = JsonDocument.Parse(IndexStatusSchema).RootElement,
        },
    ];

    public CallToolResult Call(string name, IDictionary<string, JsonElement>? arguments) => name switch
    {
        "find_files" => FindFiles(new Args(arguments)),
        "index_status" => IndexStatus(),
        _ => throw InvalidParams($"unknown tool '{name}'"),
    };

    CallToolResult FindFiles(Args args)
    {
        var query = args.String("query");
        if (string.IsNullOrWhiteSpace(query)) throw InvalidParams("query is required");
        var limit = args.Int("limit") ?? SearchRequest.DefaultLimit;
        if (limit < 1) throw InvalidParams("limit must be at least 1");
        var budgetMs = args.Int("budget_ms") ?? (int)SearchRequest.DefaultBudget.TotalMilliseconds;
        if (budgetMs < 0) throw InvalidParams("budget_ms must not be negative");

        var request = new SearchRequest(
            query,
            [args.String("root") ?? defaultRoot],
            limit,
            TimeSpan.FromMilliseconds(Math.Min(budgetMs, MaxBudgetMs)),
            args.StringArray("ext"));

        SearchResult result;
        try
        {
            result = search.Search(request, CancellationToken.None);
        }
        catch (RootNotFoundException e)
        {
            throw InvalidParams(e.Message);
        }

        var text = new StringBuilder();
        foreach (var hit in result.Hits) text.Append(hit.Path).Append('\n');
        text.Append(result.Footer());
        var structured = new FindFilesResult(result.Hits, result.TotalMatches, result.Hits.Count, (long)result.Elapsed.TotalMilliseconds, result.StoppedEarly);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = text.ToString() }],
            StructuredContent = JsonSerializer.SerializeToElement(structured, McpJson.Default.FindFilesResult),
        };
    }

    CallToolResult IndexStatus()
    {
        var status = search.GetStatus([defaultRoot]);
        var text = new StringBuilder();
        foreach (var root in status.Roots)
            text.Append($"{root.Path}: {(root.FileCount is { } n ? $"{n} files" : "not indexed")}\n");
        text.Append($"vectors complete: {(status.VectorsComplete ? "yes" : "no")}, watcher live: {(status.WatcherLive ? "yes" : "no")}");
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = text.ToString() }],
            StructuredContent = JsonSerializer.SerializeToElement(status, McpJson.Default.IndexStatus),
        };
    }

    static McpProtocolException InvalidParams(string message) => new(message, McpErrorCode.InvalidParams);

    readonly struct Args(IDictionary<string, JsonElement>? values)
    {
        public string? String(string name) => Get(name, JsonValueKind.String) is { } e ? e.GetString() : null;

        public int? Int(string name) =>
            Get(name, JsonValueKind.Number) is { } e ? e.TryGetInt32(out var v) ? v : throw InvalidParams($"{name} must be an integer") : null;

        public string[]? StringArray(string name)
        {
            if (Get(name, JsonValueKind.Array) is not { } e) return null;
            var items = new string[e.GetArrayLength()];
            var i = 0;
            foreach (var item in e.EnumerateArray())
                items[i++] = item.ValueKind == JsonValueKind.String ? item.GetString()! : throw InvalidParams($"{name} must be an array of strings");
            return items;
        }

        JsonElement? Get(string name, JsonValueKind kind)
        {
            if (values is null || !values.TryGetValue(name, out var e) || e.ValueKind == JsonValueKind.Null) return null;
            return e.ValueKind == kind ? e : throw InvalidParams($"{name} must be {kind.ToString().ToLowerInvariant()}");
        }
    }
}
