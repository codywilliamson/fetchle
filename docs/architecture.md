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

semble keeps its index fresh by re-walking the tree on every run, comparing modification times, and re-indexing only what was added, removed or changed. That's cheap for a code repo and ruinous for a home directory, where the walk is the expensive part. fetchle keeps semble's model, a cache on disk with incremental updates and a full rebuild only when index settings change, and replaces the per-run walk with cheaper change signals.

fetchle indexes names and paths, not contents, so a file's contents changing never matters. What matters is entries appearing, disappearing or being renamed, and every OS records that in the parent directory's modified time. Three sources feed changes in, cheapest first.

While the MCP server runs, watcher events apply changes as they happen. Windows uses `ReadDirectoryChangesW` and macOS uses FSEvents, and both cope with a whole home directory. Linux uses inotify, and `fs.inotify.max_user_watches` can be low enough to run out, so fetchle watches hot roots there and `fetchle doctor` reports the limit. When a watcher's buffer overflows, the root is marked dirty and rescanned.

After downtime, a journal replays what changed since a saved position, with no walk. NTFS has the USN journal, which needs admin or a helper process, like the NTFS turbo backend. FSEvents can replay from a saved event id. Linux has no equivalent.

Everywhere else, and as the safety net, fetchle checks each indexed directory's modified time and re-lists only the directories that changed. That still touches every directory but skips reading files in unchanged ones. How much it saves depends on files per directory, which hasn't been measured. A test per OS has to confirm that a directory's modified time changes on create, delete and rename before anything relies on it.

Changes never rewrite the snapshot in place. They land in an in-memory overlay of new rows and tombstones, and queries read the snapshot plus the overlay. Now and then fetchle writes a fresh snapshot with the overlay merged and swaps it in atomically, which keeps loads fast and puts each subtree back into one contiguous block of rows.

The parent-pointer store makes this cheap. Renaming a directory changes one row's name and leaves its subtree alone, where string paths would change every descendant. Embeddings are cached per unique segment, so a new file costs one encode for its new name and reuses every other vector. Lexical postings for new segments go in the overlay too.

The MCP server, or `fetchle index`, is the only writer, guarded by a lock file. The CLI only reads. When its snapshot is stale for the root being searched, it catches up that scope in memory within the query's budget and leaves the file alone.

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
