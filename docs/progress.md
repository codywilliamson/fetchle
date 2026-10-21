# v0.2 progress

Resume notes for the v0.2 "ask it like a person" run (started 2026-10-21). Branch `v0.2-ranking`. The v0.1 follow-ups are their own PR, codywilliamson/fetchle#11 (`v0.1-followups`), already merged into this branch: merge #11 first and the v0.2 diff shrinks to the ranking work. Delete this file when v0.2 is tagged.

## Done means

docs/roadmap.md's v0.2 exit check passes in CI against the held-out eval split, and the quiet-runner bench says ranking cost no latency worth noticing. Targets and the eval protocol: docs/decisions.md, the 2026-10-21 entries. Release-please cuts the tag; Cody merges.

## Eval

- `evals/queries/`: `spike.json` (12, always dev), `mined.json` (226), `blind.json` (80). Corpus: `evals/corpus/paths.txt` plus the seeded 1M `HomeCorpus`, built in memory; 667k entries after pruning.
- 228 dev, 81 held out, 9 set aside. Tune on dev only; CI gates held-out against `evals/baseline.json`.
- `dotnet run --project evals/Fetchle.Evals -c Release [-- --lexical-only | --semantic-only | --split heldout]`. Needs `dotnet build.cs model` first.
- Held-out misses by stage: `evals/failures.md`. Raw mining output and the pseudonym map live only in the session scratchpad.

| held-out | top-1 | top-3 | mrr |
|---|---|---|---|
| v0.1 substring | 4.9% | 8.6% | 0.072 |
| v0.2 (baseline) | 79.0% | 93.8% | 0.864 |
| v0.2 lexical only | 81.5% | 93.8% | 0.875 |

## Latency, quiet runners

Run 39644601983 at the commit before `perf: scan the entry pass level by level` (Windows, Ryzen 5 3600): home-1m ranked query p95 60.9 ms, indexed query 64.0 ms (target 50); picker keystroke p95 34.8 ms Windows, 31.1 Linux (target 16); embed of the 1M home corpus 225 ms Windows, 291 Linux (target 10 s). Linux external failed on glibc (fixed by pinning ubuntu-24.04).

Pending: run 39650885254 (`bench/level-pass`, the level-parallel pass alone) and 39652918353 (`v0.2-ranking`, plus chunked checksums and the picker's semantic cache). Those numbers go into the perf commits' follow-up `docs:` commit, `bench/results/`, decisions.md and the diagrams.

## State

- Done: tokenizer, lexicon (snapshot format 3), lexical retriever, own Model2Vec inference and `.fmodel`, 512-bit sign-code vectors (`vectors.fvec` format 2), fusion, picker, model shipped beside the exe and installed by the scripts, eval + gate, all five diagrams, docs, independent review (all findings fixed).
- PR #11: Codex rounds 1 and 2 done (the cap); further Codex comments there get reported, not acted on.
- Next: bench numbers into commits/docs; open the v0.2 PR; two Codex rounds, then `codex review --base main -c model='"gpt-6-astra"'`; Cody's 10-query spot check on his real index.

## How to get numbers

`gh workflow run bench.yml --ref <branch> -f external=true -f dotnet=true -f filter='*RankedBenchmarks* *EmbedBenchmarks* *IndexBenchmarks*'`. One pending run at a time (shared concurrency group). Benching one commit alone: push it to a `bench/<name>` branch with the ubuntu-24.04 pin cherry-picked. Never quote dev-box numbers as results; label them local.
