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
| `fetchle setup` | Detect agents and register fetchle with them. |
| `fetchle doctor [--fix]` | Check the install and print a fix for each failure. |
| `fetchle update` | Self-update from GitHub releases. |
| `fetchle cleanup [--yes]` | Prune stale index data and leftover files. |
| `fetchle uninstall [--yes]` | Undo everything the install receipt records. |
| `fetchle savings` | Show time and tokens saved. |
| `fetchle help [command]` | Long help with examples. |

## Search flags

| flag | default | meaning |
|---|---|---|
| `--limit <n>` | 10 (TTY), 20 (agent) | Max results. |
| `--budget <duration>` | `2s` | Stop and report what was skipped after this long. |
| `--root <path>` | all roots | Restrict to one root. Repeatable. Until `roots` ships, the default is the current directory. A root that doesn't exist fails immediately with exit 2, before any walking. |
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

## `doctor` checks

Each check prints pass, warn or fail with the exact command that fixes it. `--fix` runs the fixes.

- binary on PATH, and only once
- version vs latest release
- index age, size and root health
- watcher running (MCP server) and the inotify limit on Linux
- model file present and checksum matches
- each agent registration from the receipt still in place
- NTFS turbo availability on Windows
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

## Help text

`--help` is short: usage line and flags. `fetchle help <command>` is long, examples first, tldr-style. Until the long help is written, `fetchle help <command>` prints the command's `--help`.

Commands that aren't implemented yet print `not implemented yet` to stderr and exit 2. Man pages, the docs site and the agent skill text are all generated from the same source so they can't drift.

Agents read help too. The first screen of every command should be dense and copy-pasteable.
