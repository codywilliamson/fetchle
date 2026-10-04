# How fetchle works

fetchle ships as one executable with subcommands. `fetchle "query"` searches, `fetchle mcp` runs the stdio server, and everything else manages the index and the install. This page follows a query from the disk to the ranked answer.

## Building the index

The walker sits on `FileSystemEnumerable<T>` with a transform delegate, so entries that get filtered out never allocate a `FileInfo`. It skips reparse points and ignores inaccessible directories. Default prune rules drop `node_modules`, `.git`, Temp and cache directories before descending into them.

Pruning is the single biggest lever. On `AppData` it took the corpus from 2.6 million files and a six minute walk down to 568 thousand files and 37 seconds. It also fixed ranking: unpruned, Temp scratchpad copies of the same paths pushed real targets past rank 5,000.

Paths are stored as a tree, not as strings. Each node is a parent id plus an interned name segment, laid out as struct-of-arrays so the whole store can be memory-mapped from one file. Segments repeat heavily. The pruned `AppData` set had 4.28 million segment occurrences but only 442 thousand unique ones, so everything expensive happens per unique segment.

## Embedding segments

Each unique segment gets a vector from a static Model2Vec model, `potion-retrieval-32M` quantized to int8. Static means no transformer at query time: tokenize, look up rows, average. An encode takes about 0.05 ms under AOT.

Segments that are hashes, GUIDs or package-family suffixes get filtered or down-weighted before embedding. `Microsoft.WindowsTerminal_8wekyb3d8bbwe` tokenizes to "microsoft windows terminal 8 wekyb 3 d 8 bbwe", and that noise pushed the real Windows Terminal settings past rank 7,000.

Vectors persist as int8. Against f32 the cosine similarity averaged 0.9995 with a minimum of 0.989, and rankings moved by a few positions at most. The full first-time embed is the slow step: 17 seconds single-threaded for 442 thousand segments. It runs in parallel in the background after the walk, so lexical search works before the vectors finish.

## Ranking a query

Two retrievers each produce their own top candidates.

The lexical side scores tokens from each path. It splits camelCase, separators and digits, counts the filename twice, and adds prefix and fuzzy matching. The semantic side embeds the query and scores each path by cosine against a weighted centroid of its segment vectors: filename 2, parent 1, other ancestors 0.5. A small depth penalty then favors shallower paths.

Reciprocal rank fusion with k=60 merges the two lists.

The original plan was lexical candidates reranked by embeddings, and the spike killed it. Lexical missed the target entirely in 5 of 12 test queries, so a reranker had nothing to rescue. Running both as independent sources is what found `claude.exe` from "bundled claude binary" and `Spotify.exe` from "music streaming app". Lexical is what found Windows Terminal's config, where embeddings failed.

## Budgets

Every query carries a time budget and a result limit. When the budget expires, fetchle returns what it has plus a footer naming what it skipped, such as `312 matches, showing 10, stopped early: budget`. The MCP spike cut a 50 ms budget call off at 56 ms.

An agent never waits out a transport timeout with nothing to show. That rule matters more than raw speed.

## Staying fresh

The MCP server is long-lived, so it holds the index in memory and watches the indexed roots. The CLI loads the memory-mapped snapshot and runs the query. v0 has no separate daemon.

Watching differs by OS, and Linux is the weak spot. `FileSystemWatcher` uses inotify there, and `fs.inotify.max_user_watches` can be low enough that a whole home directory exhausts it. fetchle watches hot roots and rescans stale roots at query time. `fetchle doctor` reports the limit. macOS uses FSEvents and Windows uses `ReadDirectoryChangesW`, and both cope.

An NTFS backend that reads the MFT and USN journal is a later, optional accelerator for Windows. It needs admin, so it can never be the only path.

## Cross-platform from the first commit

The core has no Windows dependency. What varies by OS sits behind small interfaces: path comparison (Linux is case-sensitive, Windows and default macOS are not), hidden-file rules (dotfiles vs the hidden attribute), default prune lists (`AppData\Local\Temp` vs `~/.cache` vs `~/Library/Caches`), watcher strategy, and agent config locations for `fetchle setup`.

CI runs the end-to-end suite on Windows, Linux and macOS for every change. That's what keeps cross-platform true without anyone remembering to check.

## The pieces

```text
src/
  Fetchle.Core     walker, path store, prune rules, lexical + semantic ranking, budgets
  Fetchle.Watch    watcher interface, per-OS strategies
  Fetchle.Mcp      stdio server on ModelContextProtocol.Core, no hosting
  Fetchle.Cli      the executable: XenoAtom.CommandLine + XenoAtom.Terminal, all subcommands
tests/
  Fetchle.Tests    unit tests for pure logic
  Fetchle.E2E      drives the published native exe against fixture trees
evals/             query -> expected path sets, ranking quality gate
bench/             BenchmarkDotNet, results committed as JSON
demo/              VHS tapes and the Remotion project
```

Inside a project, folders group by job and namespaces match folders. In `Fetchle.Core`, `Search/` holds the `IFileSearch` seam and its request and result shapes, `Walking/` holds prune rules and path comparison, and `Naive/` holds the placeholder walker, ranker and search that the real ones replace. `Fetchle.Cli` splits into `Commands/` and `Output/`.

The MCP server uses `ModelContextProtocol.Core` with hand-written handlers. The hosted variant pulled in 31 assemblies against Core's 4, and its tool discovery relies on reflection that hasn't been proven under AOT. The pattern is in [spikes/mcp.md](spikes/mcp.md).

The terminal layer has two rules from its spike. Never start a live widget when stdout is redirected, because spinner frames leak into the output as text. Focus the picker's text box explicitly, or every keystroke gets dropped. Details are in [spikes/terminal.md](spikes/terminal.md).
