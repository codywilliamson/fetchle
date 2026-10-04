# fetchle agent guide

fetchle is a ranked file finder for agents and humans: one NativeAOT .NET 10 binary, CLI plus stdio MCP server, fully local. Read [docs/architecture.md](docs/architecture.md) before changing code, and [docs/decisions.md](docs/decisions.md) before reopening a settled choice.

## Status

Pre-alpha. The repo holds specs and spike results. v0.1 in [docs/roadmap.md](docs/roadmap.md) is the next work.

## Where things are specified

- Command behavior and output modes: [docs/specs/cli.md](docs/specs/cli.md)
- MCP tools and errors: [docs/specs/mcp.md](docs/specs/mcp.md)
- Install, receipt, update, uninstall: [docs/specs/install.md](docs/specs/install.md)
- Tests, evals, benchmarks: [docs/testing.md](docs/testing.md)

When behavior and spec disagree, fix one of them in the same change. Don't let them drift.

## Rules that came from spikes

- Never start a XenoAtom live widget when stdout is redirected. Spinner frames leak into the output.
- Focus the picker's `TextBox` explicitly with `ctx.App.Focus(input)`, or keystrokes are dropped.
- MCP uses `ModelContextProtocol.Core` with hand-written handlers. No `Microsoft.Extensions.Hosting`, no `[McpServerTool]` reflection.
- Unknown tools and bad args throw `McpProtocolException(..., McpErrorCode.InvalidParams)`.
- All JSON goes through source-generated `JsonSerializerContext`.
- Any new dependency must publish under NativeAOT with zero IL2xxx/IL3xxx warnings. Check before adding it.

## The loop

Build, test, eval, bench. A change isn't done until the native exe passes e2e, not just the JIT build.

Local Windows AOT publish needs the VS Build Tools C++ workload and `${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer` on PATH, or link fails with `'vswhere.exe' is not recognized`.

## Conventions

- Conventional commits, lowercase, one concern per commit. release-please reads them.
- Keep files small and single-purpose.
- Comments only when the why isn't obvious, lowercase and informal.
- New decisions go in [docs/decisions.md](docs/decisions.md) with the evidence that settled them.
