# What fetchle is for

fetchle finds files by description, instantly, for agents and humans, without anything leaving the machine.

The pitch to a friend: it's semble for finding files. Your agent stops running two minute `gci -Recurse` crawls. It asks once in plain words and gets the right path in milliseconds. One native .NET binary, no Python, no Everything install, no admin prompt.

## The problem is the strategy, not the walker

The incident that started this was an agent searching `AppData\Roaming\Claude`, `AppData\Local\AnthropicClaude` and `AppData\Local\Packages` with `Get-ChildItem -Recurse -Filter claude*.exe`. It timed out at 120 seconds. Raw .NET enumeration listed all 84,274 entries in those roots in 3.0 seconds, and the filtered search took 1.2.

The walker spike found the real cause, and it wasn't walk speed. `AppData\Local\AnthropicClaude` doesn't exist on that machine. Given a missing path with `-Recurse`, PowerShell doesn't error. It treats the leaf as a name to search for and crawls the parent, so the agent's command walked all 2.5 million entries of `%LOCALAPPDATA%`. With the two real roots only, the same command took 3.1 seconds. Details are in [spikes/walker.md](spikes/walker.md).

That makes the case stronger. The agent guessed a root, the guess was wrong, and the tool turned a typo into a whole-drive crawl with no error and no partial result. A faster walker doesn't fix that. The agent still has to guess roots, still pays for every directory it walks, and still gets nothing back when it runs out of time. The embeddings spike walked all of `AppData` and found 2.6 million files, mostly `node_modules`, Temp and caches. A 37 second walk after pruning, or six minutes without, is never something an agent should trigger inline.

So fetchle answers from an index that is already built, ranks results so the first one is usually right, and always returns inside a budget. When the budget runs out it says what it skipped instead of returning nothing.

## Ranking is the product

Every competitor we found filters. None of them rank. An agent asking for "the bundled claude binary" doesn't know the file is `claude-code\2.1.286\635c1867224a\claude.exe`, so substring and glob matching can't find it. In the spike, plain BM25 put that file at rank 2097. A static embedding model put it at 7, and for "music streaming app" it put `Spotify.exe` first, where lexical matching found nothing at all.

Lexical matching still wins on exact names, so fetchle fuses both. [architecture.md](architecture.md) has the pipeline.

## Privacy is structural

The index, the embedding model and the query log all live on disk. There is no telemetry and no network call in the search path. `fetchle savings` stores counts, never queries. An optional generative model for the human CLI is off by default, and it can only reach the network if the user points it somewhere.

## Goals we can measure

These are the README headline numbers, and CI holds the project to them once the harness exists.

| goal | target |
|---|---|
| warm query, p50 | under 10 ms |
| right file in top 3 | 90% of the eval set |
| cold index of a home dir, no admin | under 30 s |
| agent tokens for a "find X" task | 10x below the `gci` baseline |
| native exe | under 15 MB, model shipped beside it |

## Out of scope

Searching inside files belongs to semble and ripgrep. fetchle indexes names, paths and metadata.

fetchle is not an Everything replacement for power users who want a GUI. It's a CLI and an MCP server.

## Who it's for

Agents first, because they pay for bad search in tokens and timeouts. Humans second, through `fetchle -i`, a live picker that re-ranks as you type.

The project is also a public showcase for modern .NET: NativeAOT, SIMD, source generators, an MCP server in C#, built in the open with agents doing much of the work.
