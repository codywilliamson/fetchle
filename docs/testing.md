# How fetchle is tested

The published native exe is the product, so the end-to-end suite drives that exe and treats everything else as supporting cast. AOT builds can fail in ways the JIT build never does: trimming removes something, reflection breaks, a source generator misses a type. A test suite that only loads the DLL would pass while users get a broken binary.

## Unit tests where the logic is pure

Ranking and scoring, segment tokenizing, the noise filter for hashes and GUIDs, prune and ignore rules, the path store, query parsing, budget cutoff math, receipt read and write. These are fast, deterministic and live in `tests/Fetchle.Tests`.

Glue code gets no unit tests. If a class only wires two others together, the e2e suite covers it.

The framework is TUnit. It uses source generators and runs under NativeAOT, so the unit tests can also run against an AOT build: `dotnet publish tests/Fetchle.Tests -p:PublishAot=true`, then run the exe.

The e2e project publishes the native exe once per run, before any test starts. Set `FETCHLE_EXE` to point it at an existing build instead. Fixture trees come from `tests/Fetchle.Fixtures`. A feature the OS or user can't create, such as a case collision on NTFS or an unreadable dir when running as root, is skipped with a reason rather than passed silently.

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

**Install lifecycle.** Two layers. `tests/Fetchle.E2E` drives setup, uninstall and doctor against a temp `HOME` with `FETCHLE_DATA_DIR` and `FETCHLE_INSTALL_DIR` set. On Windows the user PATH is in the registry, which `HOME` can't redirect, so the cases that edit it run only on GitHub runners (or with `FETCHLE_E2E_ALLOW_REGISTRY=1` on a machine you've backed up) and are skipped with a reason elsewhere. `install.yml` is the full check on fresh VMs: it runs the real install script from a locally built archive, queries through PATH in a new shell, breaks PATH and the index and checks `doctor --fix`, uninstalls, asserts the temp home matches its starting state, then does it all again. Marker block insert, replace and remove, PATH string edits and the receipt are plain unit tests.

## Evals are not tests

Tests check that fetchle is correct. Evals check that it's good.

`evals/` holds query to expected-path sets over a fixture corpus big enough to be realistic. It includes the 12 queries from the embeddings spike, rewritten against fixtures. CI computes top-1 and top-3 hit rates and fails the build when either drops below the committed baseline.

A failing eval means ranking got worse. A failing test means something broke. Keeping them separate keeps the signal readable.

## Benchmarks

`bench/` has two halves. BenchmarkDotNet covers the hot paths in-process: walk throughput and query latency on the `realistic` and `dir-heavy` shapes, and snapshot open, the indexed query pass and each rebuild phase on synthetic 50k and 1M-entry trees. Quiet-runner JSON for the index benches lands in `bench/results/windows/` and `bench/results/linux/`. The older `Fetchle.Bench.*.json` at the top of `bench/results/` came from a dev box before the runners existed.

The external runner (`dotnet build.cs bench-external`) times the published exe as a process against everything it competes with, on `realistic`, `dir-heavy` and `home-1m`. `home-1m` is a 1,000,000-entry home-directory-shaped corpus from `tests/Fetchle.Fixtures/HomeCorpus.cs`: heavy-tailed files per dir, depth mostly 3 to 10 with a deeper tail, about a third of the entries under `node_modules`, `.git` and `.cache`, and the usual landmarks so `settings` hits a handful of real files. It is deterministic from a seed, cached on disk with a version marker next to the root, and used by the external runner only (too slow for BenchmarkDotNet params). Every tool runs the same query, `settings`, with one warmup and 20 timed runs; the result is process wall time, p50 and p95 by nearest rank.

| tool | what it measures |
|---|---|
| `fetchle live walk` | `fetchle settings --root <corpus>`, the parallel walker |
| `fetchle indexed query` | `fetchle settings` with `FETCHLE_DATA_DIR` isolated, after `roots add` and `index rebuild`: reads the snapshot |
| `fetchle bare walk` and `fetchle cold rebuild` | a walk that matches nothing (exit 1) next to `index rebuild` with the snapshot deleted before each run, so the 1.2x rebuild target reads straight off the two rows |
| `rg --files --iglob`, `rg -j1 ...` | `--hidden --no-ignore`, case-insensitive glob |
| `fd -H -I -i` | hidden, no ignore files, case-insensitive; the pattern is a regex on the file name |
| `Get-ChildItem -Recurse`, `cmd dir /s /b` | Windows only (pwsh, else powershell.exe) |
| `find -iname` | Linux and macOS |
| `fetchle --version`, `rg --version` | process startup floors, shape `none`, so a 25 ms target can be read against what spawning costs |

The tools are not strictly like for like. fetchle skips pruned dirs (`node_modules`, `.git`, `.cache`) and also matches on parent directory names; rg, fd, gci, dir and find walk everything and match the file name only. Put `rg` and `fd` binaries in `FETCHLE_BENCH_TOOLS` and they win over PATH. A tool that isn't installed is skipped with a warning and listed under `skipped_tools` in the JSON. A wrong exit code marks that tool `failed` in the JSON and makes the runner exit 1. A single run over 60 s stops that tool on that shape and the JSON records what ran (`skipped` or `partial`), so the job stays bounded.

Results go to `bench/results/external-windows.json` or `external-linux.json`: machine, OS, CPU count, per-shape entry counts (on disk, and what the walker sees with the default prune rules), and per tool and shape p50, p95 and every run. `FETCHLE_BENCH_SHAPES=realistic,dir-heavy` narrows a local run, `FETCHLE_BENCH_CORPUS_DIR` moves the `home-1m` corpus off the temp dir.

Numbers from a loaded dev box are never quoted: not in docs, commits or decisions. Only the files produced by the self-hosted runners count. The runner's parts that don't need a quiet machine (command construction, percentile math, the JSON shape, the home corpus) are unit tested in `tests/Fetchle.Bench.Tests`.

Agents can talk their way past a code review. They can't fake a p50.

## Where it runs

Every PR runs unit tests, e2e and evals on Windows, Linux and macOS through `dotnet build.cs ci`. It restores, builds and publishes the native exe once, then runs the suites with `--no-build` and points the e2e suite at that exe with `FETCHLE_EXE`. Run the same pipeline locally with `dotnet build.cs ci`, or one piece with `dotnet build.cs <restore|build|test|e2e|eval|publish|bench>`. The external benchmarks run on push to main and on manual dispatch, on the self-hosted runners, never on PRs; shared CI runners are too noisy for perf numbers.
