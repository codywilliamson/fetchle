# v0.2 progress

Resume notes for the v0.2 "ask it like a person" run (started 2026-10-21). Branch `v0.2-ranking`, PR to main not opened yet. The v0.1 follow-ups went into their own PR, codywilliamson/fetchle#11 (`v0.1-followups`), which should merge first; this branch deletes `SegmentMatcher`/`IndexQuery`, so its conflicts there resolve by taking the deletion. Delete this file when v0.2 is tagged.

## Done means

docs/roadmap.md's v0.2 exit check passes in CI against the held-out eval split, and the quiet-runner bench says ranking cost no latency worth noticing. Targets and the eval protocol: docs/decisions.md, the 2026-10-21 entry. Release-please cuts the tag; Cody merges.

## Eval set

- `evals/queries/`: `spike.json` (12, always dev), `mined.json` (226, from Claude Code transcript hunts, pseudonymized), `blind.json` (80, written by a subagent from the corpus alone).
- `evals/corpus/paths.txt`: 15,386 real paths behind the mined queries plus the listings around them, pseudonymized for the public repo. The eval adds the seeded 1M-entry `HomeCorpus` and prunes like the walker: 667,345 entries after pruning.
- Split by hash of id: 228 dev, 81 held out, 9 set aside (answers under pruned dirs).
- `dotnet run --project evals/Fetchle.Evals -c Release` runs dev. `--gate` and `--update-baseline` run held-out only. Never tune on held-out.
- Raw mining outputs and the pseudonym map live only in the session scratchpad, never in the repo.

## Numbers so far

| | dev top-1 | dev top-3 | dev mrr | held-out top-1 | held-out top-3 | held-out mrr |
|---|---|---|---|---|---|---|
| v0.1 substring | 12.6% | 15.6% | 0.142 | 4.9% | 8.6% | 0.072 |
| lexical only | 67.5% | 79.8% | 0.747 | not read | | |

## State

- Done: tokenizer + noise filter, lexicon in snapshot format 2, lexical retriever, 512-bit sign-code vectors + semantic pass + RRF fusion, search session (indexed and live-walked roots rank the same way), rebuild writes `vectors.fvec`, tokenizer and lexical-index diagrams.
- In flight (worker branches, merge into `v0.2-ranking`): `v0.2-model` (fetchle's own WordPiece + `.fmodel` from potion-retrieval-32M, `build.cs model`, parity test, notices) and `v0.2-picker` (`fetchle -i`).
- Next: wire the real model into the CLI, MCP, doctor and evals; tune on dev (worktree copies outranking the main checkout, dir vs file, vocabulary gaps); benches on the quiet runners; diagrams for the vector store, query flow and picker; failure table; reviews.

## How to get numbers

Same as v0.1: `gh workflow run bench.yml --ref v0.2-ranking [-f external=false] [-f filter='*IndexBenchmarks*']`, one dispatch at a time (shared concurrency group). The windows self-hosted runner was refusing to load the benchmark assemblies on 2026-10-21 (Application Control, 0x800711C7); linux numbers came through. Cody has to fix that on the box.
