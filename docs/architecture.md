# How fetchle works

fetchle ships as one executable with subcommands. `fetchle "query"` searches, `fetchle mcp` runs the stdio server, and everything else manages the index and the install. This page follows a query from the disk to the ranked answer.

## Building the index

The walker is `FastWalker`: a work-stealing pool with one worker per core, using a native directory lister on Windows and `FileSystemEnumerable<T>` elsewhere. It skips reparse points and ignores inaccessible directories. Default prune rules drop `node_modules`, `.git`, Temp and cache directories before descending into them.

Pruning is the single biggest lever. On `AppData` it took the corpus from 2.6 million files and a six minute walk down to 568 thousand files and 37 seconds. It also fixed ranking: unpruned, Temp scratchpad copies of the same paths pushed real targets past rank 5,000.

Paths are stored as a tree, not as strings. Each node is a parent id plus an interned name segment, laid out as struct-of-arrays so the whole store can be memory-mapped from one file. Segments repeat heavily. The pruned `AppData` set had 4.28 million segment occurrences but only 442 thousand unique ones, so everything expensive happens per unique segment.

The snapshot is that store on disk, `snapshot.fidx` in the index dir. A fixed header names the format version, endianness, file length and an XxHash3 of everything after it, and a section table points at the columns: parent ids, segment ids, flags, segment offsets, the UTF-8 segment blob, and the roots. Rows go roots first, then breadth-first, so a parent's id is always below its children's and the parent column never decreases. That makes a dir's children one contiguous run, found by binary search. A reader maps the file, checks the header, checksum and invariants, and then reads the columns as spans in place. Nothing is deserialized. The layout and the swap protocol are drawn in [diagrams/path-store.excalidraw](diagrams/path-store.excalidraw).

A rebuild takes the write lock, walks, writes the whole image to a temp file, flushes it and renames it over the old snapshot. On Windows that rename uses POSIX semantics, the only form that replaces a file another process still has mapped. A writer dying at any step leaves the old snapshot intact.

A query on the snapshot works per unique segment, then per entry, as the next sections describe. Because parents come before children, one forward pass sees every parent before its children, and only the hits that make a top-k ever become strings.

## Words in names

Everything a query matches is built per unique name segment, once, at rebuild. The tokenizer splits a name on anything that isn't a letter or digit, then on camelCase humps and letter/digit boundaries, and lowercases: `ConsoleHost_history.txt` is console, host, history and the extension txt, plus the whole chunk `consolehost` as a compound so a query spelled as one word still finds it. Before any of that, each chunk goes through the noise filter: hex hashes, GUIDs, random ids with many letter/digit flips, long numbers and Windows package publisher ids stay whole as one noise token. In the spike, `Microsoft.WindowsTerminal_8wekyb3d8bbwe` split into "microsoft windows terminal 8 wekyb 3 d 8 bbwe", and that junk pushed the real Windows Terminal settings past rank 7,000. Drawn in [diagrams/tokenizer.excalidraw](diagrams/tokenizer.excalidraw).

The lexicon lives in the snapshot (format 2): every token sorted by its UTF-8 bytes, postings from each token to the segments holding it, and fuzzy keys, the hash of each token and of each copy with one character deleted. Exact lookup is a binary search; a prefix is two binary searches that bound a contiguous run; one typo is a handful of key lookups, because two words within one edit always share a deletion key. Nothing scans the vocabulary or the segments. On the 667,345-entry eval corpus: 277,057 unique segments, 75,094 tokens, 979,217 postings, 457,594 fuzzy keys, 30.6 bytes per entry for the whole snapshot. Drawn in [diagrams/lexical-index.excalidraw](diagrams/lexical-index.excalidraw).

## Embedding segments

fetchle runs Model2Vec itself: potion-retrieval-32M converted once into `potion-retrieval-32M.fmodel` (BERT WordPiece vocabulary with a sorted lookup index, int8 embeddings, one scale, the mean vector), memory-mapped beside the exe. Encoding is normalize, WordPiece, drop unknown tokens, average the rows, L2-normalize: the same as Python's model2vec, which a parity test pins to cosine 0.9975 at worst over 290 strings. No Model2Vec.Net, no ML.Tokenizers.

A segment's embed text is its words and extension only: numbers, compounds and noise stay out, so a hash dir embeds as nothing and counts zero. Each vector is centered and stored as the signs of its 512 dims, 64 bytes, in `vectors.fvec`. Cutting to 64 dims instead lost the bundled claude binary in the spike's 12 queries (rank 7 to 11,242); signs of all 512 kept rankings close to float. Every entry also stores the length of its path's weighted centroid (name 2, parent 1, other ancestors 0.5), computed at rebuild from pairwise sign-code dots along the path, so a query needs no per-entry vector math.

The vectors file names the snapshot checksum and model checksum it was built from and is validated on open like the snapshot. A rebuild swaps the snapshot in first, so lexical search answers from the new tree while names embed; until the vectors match, search is lexical only. Drawn in [diagrams/vector-store.excalidraw](diagrams/vector-store.excalidraw).

## Ranking a query

Query words that are stopwords drop out, `folder` and friends become a wish for a dir, and each remaining word expands through the lexicon to the tokens it hits exactly, by prefix or within one typo, each with a weight. Their postings give every segment a byte per query word, packed into one `ulong` per segment. The semantic side embeds the query the same way, centers it and scores every segment's sign code through 64 lookup tables, one per code byte.

Then one pass over the entries. Rows are breadth-first, so each depth level is one contiguous run whose parents all sit in the level above; big levels split across threads. Each row carries the per-word best match of its ancestors down from its parent with one byte-wise max, and a running sum of segment scores for the semantic centroid. Lexical scores weight each word by idf, discount ancestor hits to half, shorten long names a little, square the share of words matched, and subtract a little per level; paths under worktree dirs (the agents' scratch copies) count half. Only files are semantic candidates: a one-word dir like `.vscode` is its whole centroid and would outscore everything sharing that word.

Each retriever keeps its top 100. Reciprocal rank fusion with k=60 merges them, the semantic list weighted 0.75, and not at all when some candidate already matched every query word. When nothing matched any word and the best meaning is under cosine 0.5, the answer is empty. Ties break on the path. A root nobody indexed is walked into an in-memory snapshot and ranked the same way, its lists merged with the snapshot's before fusion. Drawn in [diagrams/query-flow.excalidraw](diagrams/query-flow.excalidraw).

The spike's plan was lexical candidates reranked by embeddings, and the spike killed it: lexical missed the target entirely in 5 of 12 queries. On the v0.2 eval the balance is different. Lexical alone scores 81.5% top-1 and 93.8% top-3 on held-out; fused, 79.0% and 93.8%. Semantic finds things words can't ("music streaming app" to Spotify.exe) and costs a couple of first places where lexical was already right. Every miss and the stage that lost it is in [evals/failures.md](../evals/failures.md).

## The picker

`fetchle -i` opens the index once and keeps it mapped. Every keystroke goes to one background worker that cancels the search in flight, keeps only the newest waiting query and publishes a result only if nothing newer was typed, so the screen never goes back and searches never overlap. The last word counts as still being typed, so `termi` matches `terminal`. Drawn in [diagrams/picker.excalidraw](diagrams/picker.excalidraw).

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

The MCP server, or `fetchle index`, is the only writer, guarded by a lock file. The CLI only reads. In v0.1 the CLI reads the snapshot as it is, and a configured root that isn't in it yet is walked live. Catching a stale scope up in memory within the query's budget comes with the watcher work in v0.4.

## Cross-platform from the first commit

The core has no Windows dependency. What varies by OS sits behind small interfaces: path comparison (Linux is case-sensitive, Windows and default macOS are not), hidden-file rules (dotfiles vs the hidden attribute), default prune lists (`AppData\Local\Temp` vs `~/.cache` vs `~/Library/Caches`), watcher strategy, and agent config locations for `fetchle setup`.

CI runs the end-to-end suite on Windows, Linux and macOS for every change. That's what keeps cross-platform true without anyone remembering to check.

## The pieces

```text
src/
  Fetchle.Core     walker, path store, prune rules, tokenizer, lexicon, embedding model, ranking, budgets
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

Inside a project, folders group by job and namespaces match folders. In `Fetchle.Core`, `Search/` holds the `IFileSearch` seam, its request and result shapes, and `FileSearch`, which walks with `FastWalker` and keeps one private top-k per worker, merged after the walk with a total tie-break so output never depends on thread timing. `Walking/` holds `FastWalker`, the listers, prune rules and path comparison, and `Naive/` holds the placeholder ranker and top-k plus `NaiveWalker`, the single-threaded reference the tests and benches compare against. `Fetchle.Cli` splits into `Commands/` and `Output/`.

The MCP server uses `ModelContextProtocol.Core` with hand-written handlers. The hosted variant pulled in 31 assemblies against Core's 4, and its tool discovery relies on reflection that hasn't been proven under AOT. The pattern is in [spikes/mcp.md](spikes/mcp.md).

The terminal layer has two rules from its spike. Never start a live widget when stdout is redirected, because spinner frames leak into the output as text. Focus the picker's text box explicitly, or every keystroke gets dropped. Details are in [spikes/terminal.md](spikes/terminal.md).
