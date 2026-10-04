# Decisions

Newest first. Each entry says what was decided, why, and the evidence. Superseded entries stay, marked as such.

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

- Walker memory and scaling: work-stealing DFS vs the current FIFO queue, measured against rg.
- Persisted segment-vector size. 442k unique segments at 512 dims int8 is ~216 MB (arithmetic, not measured). Options: PCA to fewer dims, embed lazily for hot roots, cap per root.
- Parallel segment encoding. The spike suspects the tokenizer dominates encode time but didn't profile it.
- Which env vars each agent sets, for agent output mode detection.
- A systems domain in the docs: one high-level overview per system plus a feature map, which AGENTS.md points at instead of docs full of detail that drifts. Waiting until the real systems land, since the foundation skeleton has little to map yet.
