# Decisions

Newest first. Each entry says what was decided, why, and the evidence. Superseded entries stay, marked as such.

## 2026-10-05: native relative-open lister is the Windows default, for safety not speed

The walker lists Windows dirs with `NtCreateFile` relative to the parent's handle (`OBJ_DONT_REPARSE` plus `FILE_OPEN_REPARSE_POINT`, no backup intent) and `NtQueryDirectoryFileEx` into a 64 KB per-worker buffer. Other OSes keep the .NET `FileSystemEnumerator` lister behind the same seam.

Speed is a tie. With both listers alternating in one process (6 rounds, medians), dir-heavy (47,821 dirs) took 8.70 s native vs 9.12 s .NET at 1 worker and 1.18 s vs 1.16 s at 16 workers; realistic (3,136 dirs) 471 vs 481 ms and 62 vs 68 ms. A syscall-level micro-benchmark found no flag, buffer size (4/16/64 KB) or info class that moves a full listing by more than ~2%; a listing costs ~170 µs per dir with Defender real-time protection on, and opens relative to the parent saved only ~15% of it single-threaded. At 16 workers the gain disappears, so something below the walker serializes (guess: kernel or filter-driver contention, unprofiled without admin ETW).

It wins anyway on two measured or structural points: ~30% fewer bytes allocated per walk (17.2 MB to 12.0 MB on dir-heavy), and no full path is ever re-parsed, so a parent swapped for a junction between listing and opening can't send the walk outside the root. The .NET lister re-opens every dir by full path.

Against rg 14.x `--files` on the same corpora (process wall time, output to nul): 1.49 to 1.73 s and 170 to 229 ms, vs fetchle's in-process 1.0 to 1.2 s and 52 to 62 ms. Not like for like until the published exe is timed against it.

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

## Open

- Persisted segment-vector size. 442k unique segments at 512 dims int8 is ~216 MB (arithmetic, not measured). Options: PCA to fewer dims, embed lazily for hot roots, cap per root.
- Parallel segment encoding. The spike suspects the tokenizer dominates encode time but didn't profile it.
- Which env vars each agent sets, for agent output mode detection.
- A systems domain in the docs: one high-level overview per system plus a feature map, which AGENTS.md points at instead of docs full of detail that drifts. Waiting until the real systems land, since the foundation skeleton has little to map yet.
