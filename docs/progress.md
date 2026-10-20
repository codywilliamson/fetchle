# v0.1 progress

Resume notes for the v0.1 ship run (2026-10-20). Branch `v0.1-index`, PR codywilliamson/fetchle#7 to main. Delete this file when v0.1 is tagged.

## Done means

docs/roadmap.md's v0.1 exit check passes in CI: e2e green on windows, linux and macos; benchmark JSON committed; the install script installs, runs a query and uninstalls twice in a row on a clean runner. Targets and how they measured: docs/decisions.md, the 2026-10-20 entries.

## State

- Path store and snapshot: `src/Fetchle.Core/Index/`. Format in `SnapshotFormat.cs`, swap protocol in `IndexStore.cs`, drawn in `docs/diagrams/path-store.excalidraw`.
- Search reads the snapshot for covered roots (`IndexedFileSearch`) and walks the rest; tests pin it to the walker's exact output.
- `roots`, `index`, `setup`, `uninstall`, `doctor`, install scripts and `install.yml`: done.
- Bench: 1M home corpus, external runner, `bench.yml` on the self-hosted runners with BenchmarkDotNet on both. Results committed in `bench/results/` from run 39456621264.
- Reviews: an independent reviewer subagent, two rounds of GitHub Codex (the cap Cody set), then a local `codex review --base main -c model='"gpt-6-astra"'`. All findings fixed or answered on the PR.

## How to get numbers

`gh workflow run bench.yml --ref <branch> [-f external=false] [-f filter='*IndexBenchmarks* *RebuildBenchmarks*']`, then `gh run download <id>`. Runs share one concurrency group, so dispatch one at a time or queued jobs get cancelled. Never quote numbers from the dev PC. BenchmarkDotNet's out-of-process toolchain fails locally while agent worktrees sit under `.claude/worktrees`; `--inProcess` works for smoke checks.

## Open, for after v0.1

- Snapshot open at 1M on windows is 10.3 ms against a 5 ms target; try `PrefetchVirtualMemory` before the checksum.
- Cold rebuild on linux is 1.6-2.0x a bare walk against 1.2x; building rows inside the walk workers is the lever.
- Case-sensitive macos volumes: root management (`RootsConfig.Covers`) still folds case.
- Codex rounds past the cap raised: a top-level `null` config.json reads as no roots; install.ps1 doesn't restore `.old` if the copy itself fails; setup doesn't restore a same-file shell block after a failed receipt save; segment matching doesn't check the deadline mid-scan.
- fetchle.dev redirects to the release's install scripts aren't set up (Cloudflare).
