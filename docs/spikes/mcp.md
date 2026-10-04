# Spike: C# MCP SDK under NativeAOT

ModelContextProtocol / ModelContextProtocol.Core 2.2.0, .NET SDK 10.0.100, ILCompiler 10.0.0, win-x64.

## Verdict

Probably yes. No native binary was produced on this machine.

ILC finished on both variants with zero IL2xxx/IL3xxx warnings. Publish failed only at the final native link, because there's no MSVC linker here (vswhere finds no VS install, there are no Windows SDK libs and no lld-link). Nothing was installed. AOT-clean is proven at the compiler level. Real AOT exe size and cold start were not measured.

Both servers ran over stdio as trimmed, self-contained, single-file builds (zero trim warnings) and answered initialize, tools/list and tools/call correctly.

## Update: real NativeAOT build (after installing VS Build Tools C++)

`core/` published as a native exe: **8.65 MB**, zero warnings. Probed over stdio (`probe-core-aot.txt`): initialize, tools/list, tools/call, budget cutoff (54 ms on a 50 ms budget) and the -32602 unknown-tool error all correct, exit 0 on stdin close.

Process start to initialize response, 6 runs: 174, 40, 405, 42, 51, 85 ms. Typical ~40-85 ms vs 587-765 ms for the trimmed JIT build. The 174/405 outliers are unexplained (first run / likely Defender scanning a fresh exe), not investigated.

Gotcha: the first AOT publish failed at link with `'vswhere.exe' is not recognized` baked into the linker command (MSB3073, exit 123). Putting `${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer` on PATH fixed it. CI images normally have it; local dev may not.

`hosted/` was not rebuilt natively.

## Two variants

- `core/` uses `ModelContextProtocol.Core` only, with no DI and no hosting. It builds `McpServerOptions` with hand-written `ListToolsHandler` / `CallToolHandler` and the schema as a JSON string literal, then runs `McpServer.Create(new StdioServerTransport(options), options).RunAsync()`. This is the recommended path: no reflection in our code, and the CLI owns arg parsing and startup.
- `hosted/` uses `ModelContextProtocol` + `Microsoft.Extensions.Hosting` + `[McpServerToolType]`/`[McpServerTool]` + `.WithTools<FindFilesTool>(SpikeJson.Default.Options)`. Tool discovery goes through `AIFunctionFactory` reflection. ILC raised no warnings when given source-generated `JsonSerializerOptions`, but whether it works as a native exe is unverified.

Dependencies: core pulls in 4 assemblies (M.E.AI.Abstractions, DI.Abstractions, Logging.Abstractions, MCP.Core). Hosted pulls in 31 (Configuration.*, FileProviders, Logging.Console/EventLog/EventSource, System.Diagnostics.EventLog...). `ModelContextProtocol` itself only needs Hosting.Abstractions. The heavy set comes from `Microsoft.Extensions.Hosting`.

## Numbers

| build | size | process start to initialize response, 5 runs (ms) |
|---|---|---|
| core, trimmed single-file | 14.6 MB | 755, 612, 765, 660, 587 |
| hosted, trimmed single-file | 15.3 MB | 811, 1047, 1306, 789, 821 |
| core, framework-dependent | 2.2 MB (8 files) | 562, 248, 283, 233, 231 |
| hosted, framework-dependent | 4.3 MB (36 files) | 734, 356, 388, 361, 394 |
| core, ILC .obj before link | 48.0 MB | n/a |
| hosted, ILC .obj before link | 60.7 MB | n/a |

- Trimmed builds start slower than framework-dependent ones because trimming drops the framework's ReadyToRun code, so everything is JIT-compiled.
- The .obj files include symbols, so they aren't final exe sizes. Hosted being ~27% bigger is a fair relative hint.
- Timings include some PowerShell overhead.

## Warnings

ILC on both variants: none. Trimmed publish on both: none. `TrimmerSingleWarn=false` was set so package warnings weren't collapsed. A deliberate `Type.GetType(string).GetMethods()` produced IL2057, so warnings do surface in this setup.

Without the workaround, publish fails with:
`Microsoft.NETCore.Native.Windows.targets(142,5): error : Platform linker not found. Ensure you have all the required prerequisites ... Desktop Development for C++ workload in Visual Studio.`

`-p:IlcUseEnvironmentalTools=true` skips that check. ILC then runs fully and fails only at `"link" ... exited with code 9009`. That's how the warning report was obtained without installing tooling.

## Minimal AOT-safe pattern (Core only)

```csharp
var findFiles = new Tool {
    Name = "find_files",
    Description = "ranked file path search under the server root",
    InputSchema = JsonDocument.Parse(FindFilesSchema).RootElement, // schema as string literal
};
var options = new McpServerOptions {
    ServerInfo = new Implementation { Name = "spike-core", Version = "0.0.1" },
    Handlers = new McpServerHandlers {
        ListToolsHandler = (_, _) => ValueTask.FromResult(new ListToolsResult { Tools = [findFiles] }),
        CallToolHandler = (ctx, ct) => {
            var p = ctx.Params!;
            if (p.Name != findFiles.Name)
                throw new McpProtocolException($"unknown tool {p.Name}", McpErrorCode.InvalidParams);
            var a = p.Arguments ?? new Dictionary<string, JsonElement>();
            var query  = a.TryGetValue("query", out var q) ? q.GetString() ?? "" : "";
            var limit  = a.TryGetValue("limit", out var l) ? l.GetInt32() : 10;
            var budget = a.TryGetValue("budget_ms", out var b) ? b.GetInt32() : 2000;
            var result = FileSearch.Find(root, query, limit, budget, ct);
            return ValueTask.FromResult(new CallToolResult {
                Content = [new TextContentBlock { Text = FileSearch.Format(result) }],
                StructuredContent = JsonSerializer.SerializeToElement(result, SpikeJson.Default.SearchResult),
            });
        },
    },
};
await using var server = McpServer.Create(new StdioServerTransport(options), options);
await server.RunAsync();

[JsonSerializable(typeof(SearchResult))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
partial class SpikeJson : JsonSerializerContext;
```

Throw `McpProtocolException(..., McpErrorCode.InvalidParams)` for unknown tools. A plain `McpException` comes back as an `isError: true` tool result instead of JSON-RPC error -32602.

## Stdio behavior

`probe.ps1` sends initialize, notifications/initialized, tools/list, two tools/call and one unknown-tool call, then closes stdin. Transcripts: `probe-core.txt`, `probe-hosted.txt`.

- Both negotiated protocol `2025-06-18` and listed the tool with the correct schema. Hosted also emitted `readOnlyHint: true`.
- A 2000 ms walk of `C:\Users\Cody\source` returned `5 matches, showing 3, stopped early: budget`.
- A 50 ms budget call returned in 56 ms.
- Both exited with code 0 when stdin closed.

## Verified vs unverified

Verified:
- ILC compiles both variants with zero AOT/trim warnings.
- Trimmed builds of both work end to end over stdio.
- Source-generated STJ works for structuredContent.
- Time budget, IgnoreInaccessible and reparse-point skipping work on a real tree.
- The hosting-free setup cuts dependencies from 31 assemblies to 4.

Unverified:
- Running a real NativeAOT exe (needs the VS "Desktop development with C++" workload).
- Real AOT exe size and cold start.
- Whether `WithTools<T>` reflection works at runtime under AOT.
- Claude Code integration. `claude mcp add --scope project spike-core` wrote `.mcp.json` in this folder, but the server shows "Pending approval" until someone runs `claude` here interactively and approves it.
