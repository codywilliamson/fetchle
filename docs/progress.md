# v0.1 progress

Resume notes for the v0.1 ship run (started 2026-10-20). Branch `v0.1-index`; the PR goes from it to main. Delete this file when v0.1 is tagged.

## Done means

docs/roadmap.md's v0.1 exit check passes in CI: e2e green on windows, linux and macos; benchmark JSON committed; the install script installs, runs a query and uninstalls twice in a row on a clean runner. Targets are in docs/decisions.md (2026-10-20 entry).

## State

- Path store and snapshot: built (`src/Fetchle.Core/Index/`). Format in `SnapshotFormat.cs`, swap protocol in `IndexStore.cs`, decision and spike in docs/decisions.md.
- Search reads the snapshot for covered roots (`IndexedFileSearch`), walks the rest. Tests pin it to the walker's exact output (`tests/Fetchle.Tests/Core/Index/IndexSearchTests.cs`).
- Storage tests: every-offset truncate and corrupt sweeps, structural fuzz without the checksum, crash at each rebuild step, swap under a live mapping, lock. E2E: sampled corrupt/truncate through the exe (exit 3), rebuild killed at ten points.
- `roots add/remove/list`, `index status/rebuild`: done. `setup`, `uninstall`, `doctor`, install scripts, install CI job: a subagent on branch `v0.1-install`, merging when it reports.
- Bench: 1M home corpus (`tests/Fetchle.Fixtures/HomeCorpus*.cs`), external runner with fetchle walk/index/rebuild, rg, fd, gci, dir, find and startup floors, `bench.yml` on the self-hosted runners, plus a BenchmarkDotNet job there. Results land as artifacts; commit `bench/results/external-*.json` and `Fetchle.Bench.*.json` from runner artifacts only.

## How to get numbers

`gh workflow run bench.yml --ref <branch> [-f external=false] [-f filter='*IndexBenchmarks*']`, then `gh run download <id>`. Never quote numbers from the dev PC. BenchmarkDotNet's out-of-process toolchain fails locally while agent worktrees sit under `.claude/worktrees` (duplicate project names); `--inProcess` works for smoke checks.

## Open

- Snapshot open at 1M looked well over 5 ms in an in-process dev-box run (not a result). Trimmed blob passes on `bench/open-trim`, waiting on before/after quiet-runner numbers.
- The 25 ms warm-query target includes process start; the quiet-runner `fetchle --version` floor decides whether it's reachable.
- Excalidraw diagram of the file layout and swap protocol: `docs/diagrams/path-store.excalidraw`.
- Docs to update when behavior settles: architecture.md (index section), specs/cli.md (roots, index, exit 3, default roots), testing.md.
