using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fetchle.E2E.Harness;
using Fetchle.Fixtures;

namespace Fetchle.E2E.Mcp;

// json-rpc over stdio against `fetchle mcp`, docs/testing.md#end-to-end-against-fixture-trees
public class McpTests
{
    [Test]
    public async Task Full_conversation()
    {
        using var tree = FixtureTree.Create();
        await using var mcp = McpSession.Start(tree.Root);

        var init = await mcp.RequestAsync("initialize", new JsonObject
        {
            ["protocolVersion"] = "2025-06-18",
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = "e2e", ["version"] = "0" },
        });
        await Assert.That(init["result"]!["serverInfo"]!["name"]!.GetValue<string>()).IsEqualTo("fetchle");
        await mcp.NotifyAsync("notifications/initialized");

        var list = await mcp.RequestAsync("tools/list");
        var names = list["result"]!["tools"]!.AsArray().Select(t => t!["name"]!.GetValue<string>());
        await Assert.That(names).IsEquivalentTo(["find_files", "index_status"]);

        var find = await mcp.CallAsync("find_files", new JsonObject { ["query"] = "settings" });
        var structured = find["result"]!["structuredContent"]!;
        await Assert.That(structured["paths"]![0]!["path"]!.GetValue<string>()).IsEqualTo(tree.Full(FixtureTree.SettingsFile));
        await Assert.That(structured["total_matches"]!.GetValue<int>()).IsEqualTo(1);
        await Assert.That(structured["shown"]!.GetValue<int>()).IsEqualTo(1);
        await Assert.That(structured["stopped_early"]).IsNull();
        var text = find["result"]!["content"]![0]!["text"]!.GetValue<string>();
        await Assert.That(text).StartsWith(tree.Full(FixtureTree.SettingsFile) + "\n1 matches, showing 1, ");

        var status = await mcp.CallAsync("index_status", new JsonObject());
        await Assert.That(status["result"]!["structuredContent"]!["roots"]![0]!["path"]!.GetValue<string>()).IsEqualTo(tree.Root);

        await Assert.That(await mcp.CloseAndWaitAsync()).IsEqualTo(0);
    }

    [Test]
    public async Task Budget_cutoff_reports_stopped_early()
    {
        using var tree = FixtureTree.Create();
        await using var mcp = await McpSession.StartInitializedAsync(tree.Root);

        var find = await mcp.CallAsync("find_files", new JsonObject { ["query"] = "settings", ["budget_ms"] = 0 });
        var structured = find["result"]!["structuredContent"]!;
        await Assert.That(structured["stopped_early"]!.GetValue<string>()).IsEqualTo("budget");
        await Assert.That(find["result"]!["content"]![0]!["text"]!.GetValue<string>()).EndsWith("stopped early: budget");
    }

    [Test]
    [Arguments("""{"name":"no_such_tool","arguments":{}}""")]
    [Arguments("""{"name":"find_files","arguments":{}}""")]
    [Arguments("""{"name":"find_files","arguments":{"query":"x","limit":"ten"}}""")]
    [Arguments("""{"name":"find_files","arguments":{"query":"x","root":"/does/not/exist"}}""")]
    [Arguments("""{"name":"find_files","arguments":{"query":"x","root":"bad\u0000root"}}""")]
    [Arguments("""{"name":"find_files","arguments":{"query":"x","limit":0}}""")]
    [Arguments("""{"name":"find_files","arguments":{"query":"x","limti":5}}""")]
    [Arguments("""{"name":"index_status","arguments":{"root":"x"}}""")]
    public async Task Unknown_tools_and_bad_args_are_invalid_params(string callParams)
    {
        using var tree = FixtureTree.Create();
        await using var mcp = await McpSession.StartInitializedAsync(tree.Root);

        var response = await mcp.RequestAsync("tools/call", JsonNode.Parse(callParams)!.AsObject());
        await Assert.That(response["error"]!["code"]!.GetValue<int>()).IsEqualTo(-32602);
    }

    [Test]
    public async Task Exits_cleanly_when_stdin_closes()
    {
        using var tree = FixtureTree.Create();
        await using var mcp = await McpSession.StartInitializedAsync(tree.Root);
        await Assert.That(await mcp.CloseAndWaitAsync()).IsEqualTo(0);
    }

    sealed class McpSession : IAsyncDisposable
    {
        readonly Process _process;
        int _nextId;

        McpSession(Process process) => _process = process;

        public static McpSession Start(string workingDirectory)
        {
            var psi = new ProcessStartInfo(NativeExe.Path, ["mcp"])
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = Encoding.UTF8,
            };
            return new McpSession(Process.Start(psi)!);
        }

        public static async Task<McpSession> StartInitializedAsync(string workingDirectory)
        {
            var session = Start(workingDirectory);
            await session.RequestAsync("initialize", new JsonObject
            {
                ["protocolVersion"] = "2025-06-18",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject { ["name"] = "e2e", ["version"] = "0" },
            });
            await session.NotifyAsync("notifications/initialized");
            return session;
        }

        public Task<JsonNode> CallAsync(string tool, JsonObject arguments) =>
            RequestAsync("tools/call", new JsonObject { ["name"] = tool, ["arguments"] = arguments });

        public async Task<JsonNode> RequestAsync(string method, JsonObject? parameters = null)
        {
            var id = ++_nextId;
            await WriteAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters });
            using var cts = new CancellationTokenSource(FetchleProcess.Timeout);
            while (true)
            {
                var line = await _process.StandardOutput.ReadLineAsync(cts.Token)
                    ?? throw new InvalidOperationException($"server closed stdout: {await _process.StandardError.ReadToEndAsync()}");
                var message = JsonNode.Parse(line)!;
                // skip notifications and anything that isn't our answer
                if (message["id"]?.GetValueKind() == JsonValueKind.Number && message["id"]!.GetValue<int>() == id) return message;
            }
        }

        public Task NotifyAsync(string method) => WriteAsync(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method });

        async Task WriteAsync(JsonObject message)
        {
            await _process.StandardInput.WriteLineAsync(message.ToJsonString());
            await _process.StandardInput.FlushAsync();
        }

        public async Task<int> CloseAndWaitAsync()
        {
            _process.StandardInput.Close();
            using var cts = new CancellationTokenSource(FetchleProcess.Timeout);
            await _process.WaitForExitAsync(cts.Token);
            return _process.ExitCode;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
            _process.Dispose();
        }
    }
}
