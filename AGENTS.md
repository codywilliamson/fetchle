# fetchle agent guide

fetchle is a ranked file finder for agents and humans: one NativeAOT .NET 10 binary, CLI plus stdio MCP server, fully local. Read [docs/architecture.md](docs/architecture.md) before changing code, and [docs/decisions.md](docs/decisions.md) before reopening a settled choice.

## Status

Pre-alpha. Every spec'd command exists, but most exit 2 with "not implemented yet". Search runs end to end on a naive placeholder walker and substring match. The real walker, path store, rankers, embeddings and watcher are the next work; [docs/roadmap.md](docs/roadmap.md) orders them.

## Where things are specified

- Command behavior and output modes: [docs/specs/cli.md](docs/specs/cli.md)
- MCP tools and errors: [docs/specs/mcp.md](docs/specs/mcp.md)
- Install, receipt, update, uninstall: [docs/specs/install.md](docs/specs/install.md)
- Tests, evals, benchmarks: [docs/testing.md](docs/testing.md)

When behavior and spec disagree, fix one of them in the same change. Don't let them drift.

Docs referenced from this file stay high-level and current. Low-level detail lives in code and tests, where it can't go stale. Update a referenced doc in the same change that makes it wrong.

## Rules that came from spikes

- Never start a XenoAtom live widget when stdout is redirected. Spinner frames leak into the output.
- Focus the picker's `TextBox` explicitly with `ctx.App.Focus(input)`, or keystrokes are dropped.
- MCP uses `ModelContextProtocol.Core` with hand-written handlers. No `Microsoft.Extensions.Hosting`, no `[McpServerTool]` reflection.
- Unknown tools and bad args throw `McpProtocolException(..., McpErrorCode.InvalidParams)`.
- All JSON goes through source-generated `JsonSerializerContext`.
- Any new dependency must publish under NativeAOT with zero IL2xxx/IL3xxx warnings. Check before adding it.

## Principles

- Every task has a runnable pass/fail check: a test, build exit code, eval or benchmark. "Looks done" is not done.
- Evidence over assertion. Show the command and its output, and label guesses as guesses.
- Deep modules, local behavior. Lots of behavior behind a small interface, so most changes touch one place.
- Fast loops. Keep unit and e2e runs quick; slow suites run on main, not every iteration.
- One obvious way to do each thing. Follow the existing pattern; don't add a second one.
- Explicit and greppable over clever. Unique names, no magic wiring, no reflection.
- Write down what can't be inferred. Decisions and their evidence go in [docs/decisions.md](docs/decisions.md).
- KISS, SRP, YAGNI.
- One source of truth for knowledge (shapes, constants, specs). Duplicated code that keeps behavior local is fine; don't abstract after seeing two similar blocks.

## Performance rules

Performance is a core principle of this project. See [docs/vision.md](docs/vision.md).

- Any change to a hot path (walk, index, tokenize, embed, rank, output) comes with BenchmarkDotNet numbers before and after, `[MemoryDiagnoser]` on. Put them in the commit body.
- No allocations in per-entry loops. Use spans, `stackalloc`, pooled buffers and struct-of-arrays.
- No LINQ, reflection or boxing on hot paths.
- Compare against the best external tool for the job (rg, fd, Everything), not just our previous version.
- Guesses about why something is slow are labeled as guesses until a profile or benchmark confirms them.

## The loop

`dotnet build.cs <target>` runs every step: build, test, eval, bench. A change isn't done until the native exe passes `dotnet build.cs e2e`, not just the JIT build. `dotnet build.cs ci` runs the whole pipeline once.

## Conventions

- Conventional commits, lowercase, one concern per commit. release-please reads them.
- Keep files small and single-purpose.
- Comments only when the why isn't obvious, lowercase and informal.
- New decisions go in [docs/decisions.md](docs/decisions.md) with the evidence that settled them.
