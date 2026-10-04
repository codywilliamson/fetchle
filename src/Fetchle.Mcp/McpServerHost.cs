using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Fetchle.Mcp;

// stdio mcp server with hand-written handlers and no hosting, see docs/spikes/mcp.md
public static class McpServerHost
{
    public static async Task RunAsync(Tools tools, string version, CancellationToken cancellationToken)
    {
        var options = new McpServerOptions
        {
            ServerInfo = new Implementation { Name = "fetchle", Version = version },
            Handlers = new McpServerHandlers
            {
                ListToolsHandler = (_, _) => ValueTask.FromResult(new ListToolsResult { Tools = Tools.Definitions }),
                CallToolHandler = async (ctx, ct) =>
                {
                    var p = ctx.Params ?? throw new McpProtocolException("missing params", McpErrorCode.InvalidParams);
                    return await Task.Run(() => tools.Call(p.Name, p.Arguments, ct), ct);
                },
            },
        };
        await using var server = McpServer.Create(new StdioServerTransport(options), options);
        await server.RunAsync(cancellationToken);
    }
}
