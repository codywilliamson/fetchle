# CLI spec

Reference for every command, flag and output mode. Behavior described here is the contract the e2e suite tests.

## Commands

| command | does |
|---|---|
| `fetchle <query>` | Ranked search. The default command. |
| `fetchle -i [query]` | Live picker. Re-ranks as you type. Enter prints the selected path, Esc exits with code 1. |
| `fetchle roots [add\|remove\|list] [path]` | Manage indexed roots. |
| `fetchle index [status\|rebuild]` | Show index health or rebuild it. |
| `fetchle mcp` | Run the stdio MCP server. See [mcp.md](mcp.md). |
| `fetchle setup [--yes]` | Put fetchle on PATH and write the receipt. Agent registration arrives in v0.3. See [install.md](install.md). |
| `fetchle doctor [--fix]` | Check the install and print a fix for each failure. |
| `fetchle update` | Self-update from GitHub releases. |
| `fetchle cleanup [--yes]` | Prune stale index data and leftover files. |
| `fetchle uninstall [--yes]` | Undo everything the install receipt records. |
| `fetchle savings` | Show time and tokens saved. |
| `fetchle help [command]` | Long help with examples. |

## Search flags

| flag | default | meaning |
|---|---|---|
| `--limit <n>` | 10 (TTY), 20 (agent), 50 (picker) | Max results. At least 1, or exit 2. |
| `--budget <duration>` | `2s` | Stop and report what was skipped after this long. |
| `--root <path>` | all roots | Restrict to one root. Repeatable. With no roots configured, the default is the current directory. A root that doesn't exist or isn't a valid path fails immediately with exit 2, before any walking. |
| `--ext <ext>` | none | Filter by extension. Repeatable. |
| `--since <duration>` | none | Modified within, e.g. `3d`. |
| `--type <f\|d>` | both | Files or directories only. |
| `--json` | off | One JSON object per line. |
| `--plain` | off | Force plain output on a TTY. |

## Output modes

fetchle picks a mode from where stdout goes.

| mode | when | shape |
|---|---|---|
| pretty | stdout is a TTY | Clickable paths (OSC 8), match spans highlighted, size and age dimmed, footer with count and time. |
| plain | stdout redirected | One path per line. No escape codes, no live widgets, no footer. |
| agent | an agent env var is set, e.g. `CLAUDECODE=1` | Plain paths, then one footer line: `312 matches, showing 10, 4ms` or `..., stopped early: budget`. |
| json | `--json` | JSON lines: `{"path", "score", "size", "modified"}`, then `{"total", "shown", "elapsed_ms", "stopped_early"}`. |

When several apply, the first match wins: `--json`, `--plain`, agent env var, redirected stdout, then pretty. Only pretty mode may start live widgets.

Which env vars each agent actually sets must be confirmed before the agent mode ships. Until then only `CLAUDECODE` is checked.

Pretty mode uses synchronized output (DEC 2026) for live regions, emits OSC 9;4 taskbar progress while indexing (rate-limited), and respects `NO_COLOR`. `NO_COLOR` drops color but keeps bold, dim and links, per no-color.org.

## Picker (`-i`)

`fetchle -i [query]` opens an input box, up to 10 ranked rows (fewer on a short terminal) and a footer with the match count and per-query time. Each row is the full path with the typed words highlighted, then size and age dimmed. An initial query is searched at once and the caret starts after it. An empty query shows nothing and runs no search.

| key | does |
|---|---|
| any text, Backspace, Left/Right, Home/End | edit the query; every change re-ranks |
| Up, Down, PageUp, PageDown | move the selection through the top 50 hits (`--limit` changes the 50) |
| Enter | print the selected full path to stdout, exit 0. With no row to select it does nothing |
| Esc, Ctrl+C | print nothing, exit 1 |

The picker is pretty mode only, and `--root`, `--ext`, `--since`, `--type` and `--budget` apply to every keystroke. With stdout redirected, `--plain`, `--json` or an agent env var it writes `fetchle: the picker needs a terminal` to stderr and exits 2 without starting a widget. A snapshot that fails validation exits 3, as in search.

Typing never waits on a search. The index is opened once when the picker starts. Every change goes to one background worker that cancels the search in flight, keeps only the newest waiting query, and drops a finished result when a newer keystroke exists, so the screen only moves forward. Searches never overlap. The last word of the query counts as still being typed, so `termi` already matches `terminal`.

Target (v0.2): keystroke to the frame that shows its results, p95 under 16 ms over a 1M-entry index. `dotnet run --project bench/Fetchle.Bench -c Release -- --picker-latency` measures it on an in-memory terminal and prints p50, p95 and max. A real terminal's own paint time is not in that number.

## Roots and the index

`fetchle roots add <path>` records a directory in `<data dir>/config.json`. A root nested inside another root, or containing one, is refused with exit 2, so the snapshot never holds an entry twice. `roots remove <path>` drops it, `roots list` (or bare `roots`) prints one per line.

`fetchle index rebuild` walks every configured root and swaps in a new snapshot at `<data dir>/index/snapshot.fidx`. With no roots it exits 2. When another rebuild holds `index/write.lock`, it exits 3. `fetchle index status` (or bare `index`) prints the snapshot's age, entry and unique-name counts, size, bytes per entry and per-root entry counts and walk times; with no snapshot, or one that fails validation, it exits 3.

Search answers every root the snapshot covers, including any directory under an indexed root, from the snapshot, and walks the rest live. Results are the same paths, scores and order a live walk of the same tree would give. The snapshot is as fresh as the last rebuild: files made since don't show up, deleted ones still do until the next rebuild. A snapshot that exists but fails validation (truncated, corrupt, another version or endianness) is exit 3 with a hint to rebuild, never a partial answer.

`FETCHLE_DATA_DIR` overrides the data dir, for tests and benches.

## `doctor` checks

Each check prints one line, `pass`, `warn`, `fail` or `skip`, with the exact command that fixes it on the next line. `--fix` runs the fixes it can, then prints every check again. It runs only `fetchle setup --yes` (PATH missing or duplicated, receipt missing) and `fetchle index rebuild` (index missing, unreadable, or lacking a configured root), each once. Other fixes, such as a root that no longer exists or an outdated version, are printed for you to run.

Exit code: 0 when nothing failed (warnings and skips are fine), 5 when any check failed. With `--fix` the code reflects the checks after the fixes.

PATH is judged by what a new shell will get (the user registry value on Windows, the rc file block on Unix), so it passes right after a fix. This shell lagging behind is only a warning. The release lookup has a 3 s timeout, and `FETCHLE_RELEASE_API` points it elsewhere for tests.

- binary on PATH, and only once
- version vs latest release (warns when GitHub is unreachable)
- receipt present and readable
- index age, size and bytes per entry, and root health: every configured root exists and is in the snapshot
- watcher running (MCP server): skip until v0.4
- inotify `max_user_watches` on Linux, warns below 65,536
- model file present and checksum matches: skip until embeddings ship
- each agent registration from the receipt still in place (skip while the list is empty)
- NTFS turbo availability on Windows: skip until v0.5
- terminal capabilities detected

## `savings`

Each index build measures how long walking each root really took. Time saved per query is that walk time minus query time. Tokens saved is the size of a naive recursive listing of the scope minus what fetchle returned.

The store holds counts and totals only, never query text. Output shows totals and a sparkline.

## Exit codes

| code | meaning |
|---|---|
| 0 | Success, at least one result. |
| 1 | No results, or picker cancelled. |
| 2 | Usage error. |
| 3 | Index missing or unreadable. Run `fetchle index rebuild`. |
| 4 | Budget expired before any result. |
| 5 | Install problem: `doctor` found a failed check, or `uninstall` refused or couldn't finish. |

## Help text

`--help` is short: usage line and flags. `fetchle help <command>` is long, examples first, tldr-style. Until the long help is written, `fetchle help <command>` prints the command's `--help`.

Commands that aren't implemented yet print `not implemented yet` to stderr and exit 2. Man pages, the docs site and the agent skill text are all generated from the same source so they can't drift.

Agents read help too. The first screen of every command should be dense and copy-pasteable.
