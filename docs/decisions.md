# Decisions

Newest first. Each entry says what was decided, why, and the evidence. Superseded entries stay, marked as such.

## 2026-10-21: v0.2 fusion: semantic counts 0.75, files only, and not at all once words cover the query

Plain RRF of lexical and semantic, as the spike proposed, cut dev top-1 from 74.6% (lexical alone) to 63.2%. The cause was structural, not a weight: a one-segment path like `.vscode` or `.config` is its whole centroid, so it scored cosine 0.85 for any query sharing that one word, and the spike's 0.02-per-segment depth term favored exactly those shallow paths. Semantic alone on dev was 42.5% top-1.

Every variant below was measured on dev only (228 queries), all else fixed:

| variant | dev top-1 / top-3 / mrr |
|---|---|
| plain rrf, semantic weight 1 | 63.2 / 79.4 / 0.726 |
| semantic 0.4, ignored when a file matched every word | 72.4 / 85.1 / 0.792 |
| + semantic candidates are files only, no semantic depth term | 75.4 / 86.4 / 0.815 |
| + numbers in the embed text | 75.0 / 86.0 / 0.812 |
| + lexical votes scaled by word coverage (power 1 / 2 / 3) | 76.8 / 88.6 / 0.830, 74.6 / 85.5 / 0.808, 73.7 / 84.2 / 0.795 |
| + stopwords out of lexical terms (shipped, semantic 0.5) | 77.6 / 87.7 / 0.831 |
| semantic 0.75 instead of 0.5 (shipped) | 77.2 / 88.2 / 0.831 |
| a per-candidate cosine floor of 0.5 for semantic-only hits | 76.3 / 86.0 / 0.815 |
| semantic may only add candidates, never reorder lexical ones | 76.3 / 89.9 / 0.828 |

0.5 and 0.75 differ by one query each way; 0.75 won on top-3, the harder target. The coverage scaling and the add-only fusion were dropped as not better on dev. Lexical knobs (coverage power 1/1.5/2/3, ancestor weight 0.3-0.9, name length 0-0.15, depth 0-0.02) were swept the same way and the starting values held.

Held-out tells a less flattering story, recorded rather than tuned on: lexical alone scores 81.5 / 93.8 / 0.875, fused 79.0 / 93.8 / 0.864. Semantic earns its keep on queries no word reaches (the spike's "music streaming app"), and costs first place on a few where lexical was already right. Raising the bar for it further on held-out's say-so would be tuning on held-out.

Evidence: `dotnet run --project evals/Fetchle.Evals -c Release [-- --lexical-only | --semantic-only]`, [evals/failures.md](../evals/failures.md).

## 2026-10-21: an unrelated query returns nothing

When no path matches any query word, the best semantic hit must reach cosine 0.5 or the answer is empty (exit 1). Without it, any query came back with its nearest neighbours as if they were hits, and agents rely on exit 1 meaning "not here". Applied per candidate instead, the same floor cost 2 points of dev top-3, since real semantic rescues sit at 0.35-0.45; applied per query it costs nothing on dev.

## 2026-10-21: segment vectors are the 512 signs of the centered vector, 64 bytes

The 64-byte target ruled out 512 int8 dims (512 bytes, 216 MB at the spike's 442k segments). On the spike's 12 queries over its 567k real AppData paths, centroid+depth ranks of the target:

| storage | top-3 | top-10 | the bundled claude binary |
|---|---|---|---|
| 512 f32 | 4 | 5 | 7 |
| first 64 dims, int8 | 4 | 5 | 11,242 |
| first 128 dims, int8 | 4 | 4 | 74 |
| PCA 64, int8 | 3 | 4 | 13,772 |
| PQ 64 x 8 bits | 3 | 4 | 18 |
| 512 signs, asymmetric query | 2-3 | 5-6 | 5-6 |

Signs keep every dim, score against a float query through 64 lookup tables of 256 partial sums (one per code byte), and a path's centroid length comes from popcount dots, so no float vector is ever stored. Centering against the model's mean turned out to be a no-op for this model (its mean is tiny); it stays because it's free.

Evidence: [spikes/vector-codes](spikes/vector-codes/) over the spike's corpus; on the v0.2 eval corpus the vectors are 16.9 MB of codes plus 2.5 MB of norms for 667k entries and 277k segments.

## 2026-10-21: fetchle runs Model2Vec itself; partly supersedes the Model2Vec.Net choice of 2026-10-03

The model still ships beside the exe as decided on 2026-10-03, but fetchle no longer uses Model2Vec.Net or Microsoft.ML.Tokenizers. Model2Vec inference is a normalize, a WordPiece split and a mean of rows, so fetchle does it over its own memory-mapped `.fmodel` (vocab with a sorted lookup index, int8 rows with one global scale, the mean), converted once from the pinned Hugging Face revision by `tools/model/convert.cs` with both files' sha256 checked. No dictionary is built at load and nothing is deserialized.

Parity with Python model2vec over 290 strings: every token id equal, cosine min 0.99754, mean 0.99941. `string.Normalize(FormD)` is a silent no-op under InvariantGlobalization on Windows, so accent stripping is a generated table over Latin, Greek and Cyrillic plus arithmetic Hangul decomposition.

Search opens the model with bounds checks only and skips the full-file checksum: 33 MB per query is a real share of the latency budget, the model is read-only and sha256-checked when installed, and a corrupt row can only make a vector wrong, never a read go out of bounds. `index rebuild` and `doctor` verify the checksum. The snapshot and the vectors, which fetchle writes, keep the full check on every open.

## 2026-10-21: the lexicon lives in the snapshot, format 2

Sorted tokens, postings and fuzzy deletion keys are seven more sections of `snapshot.fidx`, bounds-checked on open like the rest. They're built per unique segment at rebuild and are what makes prefix and typo matching sublinear. On the 667k-entry eval corpus the whole snapshot is 19.5 MB, 30.6 bytes per entry, still under v0.1's 40. A v1 snapshot reads as another version: exit 3, rebuild.

## 2026-10-21: the eval corpus is pseudonymized; raw transcripts never enter the repo

The repo is public and the mined paths include client and project names. A curator subagent replaced every project and client dir name under `source/` (except fetchle and a few generic words like health or infra, which also occur inside unrelated names), dropped the Obsidian vault and anything secret-shaped, and grepped the result for every real name; the mapping stays in the session scratchpad. Public third-party names (npm, NuGet, app vendors) stay as they are. Cody can veto any of this before merge.

## 2026-10-21: v0.2 ranking targets and the eval protocol

Proposed before building, kept unless a measurement says no. Latency numbers come only from the quiet self-hosted runners (`bench.yml`), on the published AOT exe, process wall time, 20 runs after a warmup, nearest-rank percentiles.

| target | number | why |
|---|---|---|
| held-out top-3 hit rate | >= 90% | the vision's headline number, and the roadmap's v0.2 exit check |
| held-out top-1 hit rate | >= 70% | an agent mostly reads the first path; below 70% it has to read three every time |
| held-out MRR | reported per release, no floor | tracks movement inside the top 10 that top-1/top-3 hide |
| CI gate | fails below the committed held-out baseline on top-1, top-3 or MRR | ranking can only move up without someone deciding otherwise |
| warm ranked query, `home-1m` | p95 <= 50 ms | the v0.1 target for the same corpus; substring measured 26.6 ms windows and 13.4 ms linux, ranking may spend the difference and no more |
| segment vectors on disk | <= 64 bytes per unique segment | 512 int8 dims is 512 bytes, 216 MB at the spike's 442k segments. 64 bytes is 28 MB, small enough to map whole |
| background embed, `home-1m` | <= 10 s wall, lexical answers before it finishes | the spike's single-threaded encode took 17 s for 442k segments; a rebuild must not wait on it |
| `fetchle -i` keystroke to repaint | p95 <= 16 ms over the 1M index | one 60 Hz frame |

Eval protocol. Queries come from two sources, kept apart so neither leaks into the other: real file hunts mined from Cody's Claude Code transcripts (the search command, the user's intent, and the path the agent used next), and queries a separate subagent writes from the fixture corpus alone, without ever seeing ranker output. Target 150+ judged queries. Each query gets a stable id; the split is by a hash of the id, about two thirds dev and one third held out, stratified by source. Tuning only ever reads dev results. The CI gate, the baseline and every reported number come from held-out. Raw transcripts never enter the repo: only the derived query, the expected paths and the corpus paths needed to reproduce them, with secret-looking names and `~/.ssh`-style dirs filtered out.

The corpus is built from a path list straight into a snapshot in memory, with no files on disk, so the eval can be large (mined real paths, the paths seen in transcript listings as real distractors, and the seeded `HomeCorpus` filler) and still run in CI on three OSes.

Every held-out miss gets a row in `evals/failures.md` saying which stage lost it: tokenizer, noise, lexical, semantic, fusion or depth. Fixes target a class of failure and must hold on dev; a fix aimed at one query is not allowed.

## 2026-10-20: v0.1 measured against its targets; two held only on one os

Quiet self-hosted runners (Randy-PC1, Ryzen 5 3600: Windows 11 26300, and Ubuntu 24.04), run 39456621264 at fec3a98, process wall time over 20 runs after a warmup, nearest-rank percentiles. Raw data: `bench/results/external-*.json`, `bench/results/{windows,linux}/`.

| target | windows | linux | held? |
|---|---|---|---|
| warm query, dir-heavy, p95 <= 25 ms | 16.6 ms (rg 962 ms) | 7.7 ms (rg 572 ms) | yes |
| warm query, 1M home corpus, p95 <= 50 ms | 26.6 ms (rg 1,895 ms) | 13.4 ms (rg 710 ms) | yes |
| snapshot open, 1M entries, <= 5 ms (BenchmarkDotNet mean) | 10.3 ms | 3.6 ms | linux only |
| on disk <= 40 B per entry | 15.2-27.0 | 15.2-27.0 | yes |
| cold rebuild <= 1.2x a bare walk (p50) | 1.04x dir-heavy, 1.07x 1M, 1.23x realistic | 1.57x, 1.65x, 1.99x | windows, barely |

For scale, the process floor on that box: `fetchle --version` 13.7 ms p50 on windows and 9.7 ms on linux, `rg --version` 6.2 and 1.0 ms.

Open on windows: mapping and checksumming the synthetic 1M-entry snapshot (every name unique, so larger than a real one) costs nearly three times what it does on linux on the same cpu. Guess, not profiled: page-fault cost of first touch through the mapping. Real snapshots are smaller (11 MB for the 653k-entry home corpus), and the whole 1M query still lands at 26.6 ms p95 including process start, so the miss doesn't move the headline target. Next lever to measure: `PrefetchVirtualMemory` before the checksum pass.

Rebuild on linux: the walk there is fast (152 ms for 653k entries), so the fixed costs show. BenchmarkDotNet at 1M entries puts them at assemble 98 ms, image 33 ms, check 3.6 ms and write plus fsync plus rename 68 ms. Assemble already dropped from 258 ms (interning into one arena). Getting to 1.2x would need building rows inside the walk workers instead of after them, which is a walker change and a v0.2 job. The fsync stays: the snapshot is derived data, but dropping the flush before the rename would trade a crash-consistent swap for a benchmark ratio, and a storage engine doesn't make that trade.

Also learned: Smart App Control (`VerifiedAndReputableDesktop`) on the windows runner blocked the downloaded exe and even locally built dlls with "An Application Control policy has blocked this file" (0x800711C7) until it was turned off; nothing in the repo can work around that.

## 2026-10-20: pin the sdk to an exact version

`global.json` pins 10.0.401 with `rollForward: disable`. With `10.0.400` plus `latestFeature`, setup-dotnet installed 10.0.402 the day it shipped, the SDK-added ILLink package moved from 10.0.12 to 10.0.13, and locked restore failed on all three CI runners while the dev box still had 10.0.401. An exact pin makes the SDK move only in a commit that also refreshes the lock files.

## 2026-10-20: v0.1 index targets, and the snapshot swap protocol

Targets, proposed before building and kept unless a measurement says no. All exe numbers are process wall time including startup, 20+ runs, p50/p95, on the quiet self-hosted runners, never on a loaded dev box.

| target | number | why |
|---|---|---|
| warm query, built index, dir-heavy (47,821 dirs) | p95 <= 25 ms | rg walks it in 887 ms, so this is the 10x+ the index exists for |
| warm query, built index, 1M-entry home corpus | p95 <= 50 ms | proposed here: the per-entry pass is linear, so 15x the entries can't stay at 25 ms |
| snapshot open, 1M entries | <= 5 ms | memory-mapped, checksum and invariants verified, nothing deserialized |
| on-disk size | <= 40 bytes per entry | parent id + segment id + flags is 9 bytes, the rest is unique segment text |
| cold `fetchle index rebuild` | <= 1.2x a bare walk of the same roots | building and writing must not cost more than a fifth of the walk |

Swap protocol. The writer writes `snapshot.fidx.tmp`, flushes it to disk, then renames it over `snapshot.fidx`. On Windows a plain `File.Move(..., overwrite: true)` fails with access denied while any reader has the old file mapped (spike, Windows 11 26200, NTFS). `SetFileInformationByHandle(FileRenameInfoEx)` with `FILE_RENAME_FLAG_REPLACE_IF_EXISTS | FILE_RENAME_FLAG_POSIX_SEMANTICS` replaced it three times in a row while the old view stayed readable and kept its old bytes. Readers open with `FileShare.Read | FileShare.Delete`. On Unix `rename(2)` already has these semantics. One writer at a time holds `index/write.lock` open with `FileShare.None`; the OS drops it if the writer dies.

Reads trust nothing. Open checks magic, version, endianness, the file length, an XxHash3 over the whole file and every structural invariant (section bounds, parent before child, ids in range) before answering. Any failure is exit 3, never a crash or a partial answer.

A configured root that isn't in the snapshot is walked live rather than failing, so `roots add` never breaks search. A snapshot that exists but can't be read is exit 3. The snapshot can be stale until the next rebuild; freshness is v0.4.

## 2026-10-05: native relative-open lister is the Windows default, for safety not speed

The walker lists Windows dirs with `NtCreateFile` relative to the parent's handle (`OBJ_DONT_REPARSE` plus `FILE_OPEN_REPARSE_POINT`, no backup intent) and `NtQueryDirectoryFileEx` into a 64 KB per-worker buffer. Other OSes keep the .NET `FileSystemEnumerator` lister behind the same seam.

Speed is a tie. With both listers alternating in one process (6 rounds, medians), dir-heavy (47,821 dirs) took 8.70 s native vs 9.12 s .NET at 1 worker and 1.18 s vs 1.16 s at 16 workers; realistic (3,136 dirs) 471 vs 481 ms and 62 vs 68 ms. A syscall-level micro-benchmark found no flag, buffer size (4/16/64 KB) or info class that moves a full listing by more than ~2%; a listing costs ~170 µs per dir with Defender real-time protection on, and opens relative to the parent saved only ~15% of it single-threaded. At 16 workers the gain disappears, so something below the walker serializes (guess: kernel or filter-driver contention, unprofiled without admin ETW).

It wins anyway on two measured or structural points: ~30% fewer bytes allocated per walk (17.2 MB to 12.0 MB on dir-heavy), and no full path is ever re-parsed, so a parent swapped for a junction between listing and opening can't send the walk outside the root. The .NET lister re-opens every dir by full path.

Against rg, the published AOT exe on a quiet machine (Ryzen 5 3600, 12 threads, Defender on, 2026-10-08), same query `settings` on both, 20 runs, p50/p95: realistic 64/70 ms vs rg 65/73 ms, dir-heavy 943/1,000 ms vs rg 887/960 ms (rg `-j1`: 197 ms and 2.81 s). A tie on realistic trees and ~6% behind on directory-heavy ones; the earlier "1.9x faster" came from a run with a game loading the other machine. The index, not the walk, is where fetchle has to pull ahead.

Also measured and dropped: parking idle workers on a semaphore (dir-heavy 0.110 to 0.170 of naive's time, worse), more workers than logical cores (32 and 64 slower than 16), and hoisting the include delegate (no allocation change).

## 2026-10-04: one build.cs instead of build.ps1 and build.sh

A .NET 10 file-based app runs every step on every OS, so the two scripts can't drift apart, and its output is structured: a header per step, the command it ran, time per step and a summary table. `dotnet build.cs ci` restores, builds and publishes once, then runs unit, e2e (against the published exe through `FETCHLE_EXE`) and evals without rebuilding. Locally it took 1m03s end to end.

## 2026-10-04: NuGet lock files, except for Fetchle.Cli

Lock files let CI cache restores and run `--locked-mode`. Fetchle.Cli is excluded because `PublishAot` adds the host RID's ILCompiler pack to its restore, so its lock file would differ on each OS. Listing all five release RIDs instead pulled 1.9 GB of runtime packs. Cli's third-party packages stay pinned through Fetchle.Tests' lock file, which references it. A new SDK patch changes the SDK-added ILLink package version, so lock files need a refresh when the SDK moves.

## 2026-10-04: MCP tool schemas generated from C# records

`FindFilesArgs` and `IndexStatusArgs` are the single source for the advertised schema (through `JsonSchemaExporter` on the source-generated type info) and for validation (unknown members disallowed, nullable annotations respected). It's AOT-clean. JSON-string schemas could drift from the handler code.

## 2026-10-03: TUnit for unit and e2e tests

TUnit 1.72.16 publishes under NativeAOT with zero IL/AOT warnings (warnings are errors in this repo). The AOT test exe is 28 MB and ran all 48 unit tests in 18 ms. No need for the xUnit v3 fallback.

Evidence: `dotnet publish tests/Fetchle.Tests -c Release -r win-x64 -p:PublishAot=true`, exit 0, then `Fetchle.Tests.exe`: `total: 48, failed: 0`.

## 2026-10-03: budgets are a per-entry deadline check, not a timer

`CancellationTokenSource.CancelAfter` fires on a timer thread, so `--budget 0ms` on a small tree finished the whole walk before the cancel landed. The walker now checks a `Stopwatch` deadline on every entry, which makes cutoffs deterministic and testable. Whether that check shows up in walk throughput is unmeasured.

## 2026-10-03: search and commands are two parser apps

XenoAtom.CommandLine won't accept root positionals when subcommands are declared (`fetchle foo` fails with "Unknown command or option"). `FetchleApp` checks `args[0]`: a known command name goes to the commands app, anything else to the search app. A query that is exactly a command name, like `fetchle index`, runs the command.

## 2026-10-03: parallel walker on FileSystemEnumerable, missing roots are errors

A 16-thread walker on non-recursive `FileSystemEnumerable` ran 2.4x faster than single-threaded on 85k entries (0.51 s vs 1.23 s) and 2.5 to 3x faster on a 7.7M entry home dir (60 s). It's ~12x faster than `gci` with ~9x less memory. ripgrep still beats it by 1.3 to 1.5x and uses 17 MB against our ~100 MB, because rg scales better across threads. Next step is per-worker LIFO deques with work stealing; that the design closes the gap is a guess.

A missing root fails immediately and is never treated as a search pattern. That PowerShell behavior is what caused the original 120 s timeout. Reparse points are skipped by default; junctions are not followed.

Evidence: [spikes/walker.md](spikes/walker.md).

## 2026-10-03: ship potion-retrieval-32M int8 beside the exe

The exe stays at ~5.6 MB and the 32 MB model ships in the same archive. Embedding the model gives a 39 MB exe but still needs a temp extract, because Model2Vec.Net only loads from a directory. retrieval-32M beat base-8M on descriptive queries. int8 matched f32 rankings at a quarter of the size. Cold start ~110 ms. Fallback if size wins: base-8M int8 embedded, 13.6 MB, ~65 ms.

Both potion models are MIT on Hugging Face (checked 2026-10-04), so redistributing them is fine. The model cards ask for a citation of Model2Vec, so the release archive needs a third-party notices file crediting it and the model. potion-base-8M was distilled from `BAAI/bge-base-en-v1.5`; that teacher's license wasn't checked.

Evidence: [spikes/embeddings.md](spikes/embeddings.md), [potion-retrieval-32M](https://huggingface.co/minishlab/potion-retrieval-32M), [potion-base-8M](https://huggingface.co/minishlab/potion-base-8M).

## 2026-10-03: two retrievers fused by RRF, not lexical-then-rerank

Lexical missed the target entirely in 5 of 12 spike queries, so reranking its candidates couldn't recover them. Running lexical and semantic as independent candidate sources and fusing with RRF found targets that each one alone missed.

Evidence: [spikes/embeddings.md](spikes/embeddings.md).

## 2026-10-03: prune by default

Unpruned `AppData` was 2.6M files with a six minute walk, and Temp copies drowned real results. Pruning `node_modules`, `.git`, Temp and cache dirs cut it to 568k files and 37 s.

Evidence: [spikes/embeddings.md](spikes/embeddings.md).

## 2026-10-03: MCP on ModelContextProtocol.Core without hosting

Zero AOT warnings, 8.65 MB native exe, 40 to 85 ms to the first response. The hosted variant pulled 31 assemblies against Core's 4 and relies on reflection-based tool discovery that hasn't been proven at runtime under AOT.

Evidence: [spikes/mcp.md](spikes/mcp.md).

## 2026-10-03: XenoAtom for terminal and arg parsing, not Spectre.Console

Zero AOT warnings, 5.2 MB exe, native OSC 8 and synchronized output, clean plain text when redirected. fetchle owns three workarounds: skip live widgets when redirected, focus the picker's input explicitly, rate-limit OSC 9;4. Also file the startup detection cost upstream.

Evidence: [spikes/terminal.md](spikes/terminal.md).

## 2026-10-03: name is fetchle

Free on NuGet, npm, crates, PyPI, Homebrew, scoop, GitHub and .dev at the time of checking. fetchle.dev is registered on Cloudflare. Repo lives at `codywilliamson/fetchle` for now. Rejected: fwip (fwip.dev registered, fwip.app is a local-first dev tools product), scout (overloaded), snaffle (Snaffler is a known C# file-finding pentest tool).

Evidence: [spikes/naming.md](spikes/naming.md).

## 2026-10-03: MIT license

Matches the ecosystem: semble, Model2Vec.Net, fastfind and MFTLib are all MIT.

## 2026-10-20: uninstall hands the running exe's dir to a detached cmd

Windows can't delete a running exe, so uninstall deletes everything else, then spawns `cmd` that retries `rmdir` for about a minute. Moving the exe to `.old` doesn't help, the file is still in the dir being deleted. Two traps found on the way: `cmd` dies when its redirected stdout pipe breaks (a `for` loop echoes into it) so the script starts with `@echo off`, and a second `for` after a first nests inside it unless each is parenthesized. The child's stdio is redirected and closed so a test harness waiting on our stdout doesn't wait on it too. `doctor` judges PATH from the registry value or rc file, not the process env, so a fix passes immediately instead of after a new shell.

Evidence: `scripts/ci/install-lifecycle.ps1` run locally on Windows, two cycles; the E2E case `Uninstall_removes_the_dir_that_holds_its_own_exe`.

## Open

- Which env vars each agent sets, for agent output mode detection.
- A systems domain in the docs: one high-level overview per system plus a feature map, which AGENTS.md points at instead of docs full of detail that drifts. Waiting until the real systems land, since the foundation skeleton has little to map yet.
