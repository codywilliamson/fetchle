# Roadmap

Each iteration is a vertical slice sized for a day or two of agent work. It ends in a tagged release, a demo tape, and a post. An iteration is done when its exit check passes in CI, not when the code looks finished.

## v0.1: faster than gci

- Repo skeleton, CI matrix, release-please, AOT publish, install scripts.
- Walker with default prune rules, path store, memory-mapped snapshot.
- `fetchle <query>` with substring and glob matching, plain and pretty output, budgets.
- `fetchle index`, `fetchle roots`, `fetchle doctor` (basic checks).
- Benchmark against `gci`, `dir /s /b` and fd on a fixture corpus.

Exit check: e2e green on three OSes, benchmark JSON committed, install script works twice in a row on a clean runner.

Post: the 120 s timeout screenshot next to fetchle's time.

## v0.2: ask it like a person

- Segment tokenizer and noise filter for hashes, GUIDs and package-family suffixes.
- Lexical retriever with prefix and fuzzy matching.
- Model2Vec semantic retriever over persisted int8 segment vectors.
- RRF fusion, depth prior.
- Eval set and the CI gate on top-1 and top-3.
- `fetchle -i` live picker.

Exit check: top-3 hit rate at or above 90% on the eval set.

Post: "music streaming app" returning `Spotify.exe`.

## v0.3: agents

- `fetchle mcp` with `find_files` and `index_status`.
- `fetchle setup` for Claude Code first, then the rest.
- Agent output mode, skill file, `fetchle savings`.

Exit check: an agent transcript test where a "find X" task costs at least 10x fewer tokens than the `gci` baseline.

Post: side-by-side agent transcripts.

## v0.4: always fresh

- Watchers per OS, stale-root rescan, inotify limit handling.
- `update`, `cleanup`, `uninstall` driven by the receipt.

Exit check: freshness e2e suite green on three OSes. Full install lifecycle test green.

## v0.5: NTFS turbo

- Optional MFT and USN backend on Windows, behind the same index interface.
- `doctor` reports whether it's available and what it would save.

Exit check: cold index of a full NTFS volume measured and published.

## Later

- Opt-in local generative model for the human CLI.
- winget, scoop and brew tap.
- Extract the terminal layer into its own library if it grows into one.
