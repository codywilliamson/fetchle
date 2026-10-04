# How fetchle is tested

The published native exe is the product, so the end-to-end suite drives that exe and treats everything else as supporting cast. AOT builds can fail in ways the JIT build never does: trimming removes something, reflection breaks, a source generator misses a type. A test suite that only loads the DLL would pass while users get a broken binary.

## Unit tests where the logic is pure

Ranking and scoring, segment tokenizing, the noise filter for hashes and GUIDs, prune and ignore rules, the path store, query parsing, budget cutoff math, receipt read and write. These are fast, deterministic and live in `tests/Fetchle.Tests`.

Glue code gets no unit tests. If a class only wires two others together, the e2e suite covers it.

TUnit is the candidate framework. It uses source generators and runs under NativeAOT, so the unit tests could also run against an AOT build.

## End-to-end against fixture trees

Each e2e test builds a fixture tree in a temp directory from a seeded manifest, so failures reproduce exactly. The fixture deliberately includes the things that break walkers:

- directory junctions and a symlink loop
- unicode and emoji file names
- a path longer than 260 characters
- a directory the test user can't read
- two files differing only by case (on Linux, where that's legal)
- a deep `node_modules` that must be pruned

The suites, all in `tests/Fetchle.E2E`:

**CLI output.** Snapshot tests of the same query in pretty, plain, agent and json modes. Pretty mode renders through XenoAtom's `InMemoryTerminalBackend`, which the terminal spike already used to drive the picker with injected keys. Plain mode asserts zero escape bytes, which catches the spinner leak the spike found.

**MCP conversations.** JSON-RPC over stdio against `fetchle mcp`: initialize, tools/list, `find_files`, a budget cutoff, an unknown tool returning -32602, clean exit on stdin close. The probe script from the MCP spike is the starting point.

**Freshness.** Create, rename and delete files under a watched root, then assert a query sees each change within a bounded time.

**Install lifecycle.** Point `HOME` and `USERPROFILE` at a temp dir. Run the install twice and assert there's one PATH entry and one marker block per agent. Run uninstall and assert the temp home matches its starting state. GitHub runners are fresh VMs, so they're the clean machines this needs.

## Evals are not tests

Tests check that fetchle is correct. Evals check that it's good.

`evals/` holds query to expected-path sets over a fixture corpus big enough to be realistic. It includes the 12 queries from the embeddings spike, rewritten against fixtures. CI computes top-1 and top-3 hit rates and fails the build when either drops below the committed baseline.

A failing eval means ranking got worse. A failing test means something broke. Keeping them separate keeps the signal readable.

## Benchmarks

`bench/` uses BenchmarkDotNet for the hot paths: walk throughput, query latency, segment encode. Results are committed as JSON, the README chart renders from that JSON, and CI fails on a regression past a set threshold.

Agents can talk their way past a code review. They can't fake a p50.

## Where it runs

Every PR runs unit tests, e2e and evals on Windows, Linux and macOS. Benchmarks run on main and on release tags, because shared CI runners are too noisy for per-PR perf gates.
