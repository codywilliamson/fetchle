using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Fetchle.Core.Search;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Fetchle.Mcp;

public sealed class Tools(IFileSearch search, string defaultRoot)
{
    public const int MAX_BUDGET_MS = 25_000;

    public static readonly List<Tool> Definitions =
    [
        new()
        {
            Name = "find_files",
            Description = "Ranked file and directory path search. Returns paths plus a footer with match count, time and whether the budget cut it short.",
            InputSchema = ToolSchema.For(McpJson.Default.FindFilesArgs),
        },
        new()
        {
            Name = "index_status",
            Description = "Roots, file counts, last full scan, vector and watcher state. Use it to decide whether to trust a miss.",
            InputSchema = ToolSchema.For(McpJson.Default.IndexStatusArgs),
        },
    ];

    public CallToolResult Call(string name, IDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        switch (name)
        {
            case "find_files":
                return FindFiles(Parse(arguments, McpJson.Default.FindFilesArgs), cancellationToken);
            case "index_status":
                Parse(arguments, McpJson.Default.IndexStatusArgs);
                return IndexStatus();
            default:
                throw InvalidParams($"unknown tool '{name}'");
        }
    }

    CallToolResult FindFiles(FindFilesArgs args, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(args.Query))
        {
            throw InvalidParams("query must not be blank");
        }
        if (args.Limit < 1)
        {
            throw InvalidParams("limit must be at least 1");
        }
        if (args.BudgetMs < 0)
        {
            throw InvalidParams("budget_ms must not be negative");
        }
        // element nullability isn't covered by RespectNullableAnnotations
        if (args.Ext is { } ext && Array.IndexOf(ext, null) >= 0)
        {
            throw InvalidParams("ext must not contain null");
        }

        var request = new SearchRequest(
            args.Query,
            [args.Root ?? defaultRoot],
            args.Limit,
            TimeSpan.FromMilliseconds(Math.Min(args.BudgetMs, MAX_BUDGET_MS)),
            args.Ext);

        SearchResult result;
        try
        {
            result = search.Search(request, cancellationToken);
        }
        catch (InvalidRootException e)
        {
            throw InvalidParams(e.Message);
        }

        var text = new StringBuilder();
        foreach (var hit in result.Hits)
        {
            text.Append(hit.Path).Append('\n');
        }
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
        {
            text.Append($"{root.Path}: {(root.FileCount is { } n ? $"{n} files" : "not indexed")}\n");
        }
        text.Append($"vectors complete: {(status.VectorsComplete ? "yes" : "no")}, watcher live: {(status.WatcherLive ? "yes" : "no")}");
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = text.ToString() }],
            StructuredContent = JsonSerializer.SerializeToElement(status, McpJson.Default.IndexStatus),
        };
    }

    static T Parse<T>(IDictionary<string, JsonElement>? arguments, JsonTypeInfo<T> typeInfo)
    {
        var json = arguments is null
            ? "{}"u8.ToArray()
            : JsonSerializer.SerializeToUtf8Bytes(arguments, McpJson.Default.IDictionaryStringJsonElement);
        try
        {
            return JsonSerializer.Deserialize(json, typeInfo) ?? throw InvalidParams("arguments must be an object");
        }
        catch (JsonException e)
        {
            throw InvalidParams(e.Message);
        }
    }

    static McpProtocolException InvalidParams(string message) => new(message, McpErrorCode.InvalidParams);
}
