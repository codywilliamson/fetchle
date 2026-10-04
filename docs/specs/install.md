# Install spec

Reference for install, update, setup, cleanup and uninstall. The rule behind all of them: the scripts stay thin, the binary does the work, and a receipt records every change so the binary can undo exactly what it did.

## Install channels

| channel | command | when |
|---|---|---|
| script, Windows | `irm https://fetchle.dev/install.ps1 \| iex` | v0.1 |
| script, Unix | `curl -fsSL https://fetchle.dev/install.sh \| sh` | v0.1 |
| .NET tool | `dotnet tool install -g fetchle` | v0.1, ships the native binary via .NET 10 platform-specific tools |
| one-shot | `dnx fetchle mcp` | v0.1, handy in MCP configs |
| winget, scoop, brew tap | via release workflow actions | after there are users |

fetchle.dev is on Cloudflare. The install URLs redirect to the scripts in the latest GitHub release, so the scripts are versioned with the binary.

## What the scripts do

1. Detect OS and arch. Fail with a clear message on anything unsupported.
2. Download the release archive and its `.sha256` from GitHub releases.
3. Verify the checksum. Stop on mismatch, delete the download.
4. Extract the exe and the model file into the install dir.
5. Run `fetchle setup`, which handles PATH, the receipt and agent registration.

Re-running the script upgrades or repairs. It never adds a second PATH entry.

| OS | install dir | data dir (index, savings) |
|---|---|---|
| Windows | `%LOCALAPPDATA%\fetchle\bin` | `%LOCALAPPDATA%\fetchle` |
| Linux | `~/.local/share/fetchle/bin` | `$XDG_DATA_HOME/fetchle`, else `~/.local/share/fetchle` |
| macOS | `~/.local/share/fetchle/bin` | `~/Library/Application Support/fetchle` |

## The receipt

`<data dir>/receipt.json`, written by `setup`, read by `update`, `doctor`, `cleanup` and `uninstall`.

```json
{
  "version": "0.1.0",
  "channel": "script",
  "install_dir": "C:\\Users\\Cody\\AppData\\Local\\fetchle\\bin",
  "path_edit": { "kind": "windows-user-path", "entry": "C:\\Users\\Cody\\AppData\\Local\\fetchle\\bin" },
  "agents": [
    { "agent": "claude-code", "kind": "mcp", "file": "C:\\Users\\Cody\\.claude.json" },
    { "agent": "claude-code", "kind": "skill", "file": "C:\\Users\\Cody\\.claude\\skills\\fetchle\\SKILL.md" }
  ]
}
```

On Unix, `path_edit` names the shell rc file and the marker block it added.

## `setup`

Detects Claude Code, Codex, Cursor, VS Code, OpenCode and Gemini CLI. Shows a checklist, then for each selected agent registers the MCP server and installs a skill that tells the agent to call fetchle instead of `gci` or `find`.

Every block it writes into a file someone else owns sits between markers:

```text
# >>> fetchle >>>
...
# <<< fetchle <<<
```

Re-runs replace the block in place. Without a TTY, or with `--yes`, setup takes the defaults and skips prompts.

## `update`

Reads the receipt's `channel`. For `script`, it downloads the new release, verifies the checksum and swaps the exe atomically. Windows can rename a running exe but not delete it, so the old one moves to `fetchle.exe.old` and the next run deletes it.

For any other channel it prints that manager's command and exits 0: `brew upgrade fetchle`, `winget upgrade fetchle`, `dotnet tool update -g fetchle`.

## `cleanup`

Lists what it would remove with sizes: index data for roots no longer configured, `.old` binaries, stale caches. It deletes nothing without `--yes`.

## `uninstall`

Removes exactly what the receipt lists: the PATH entry, each marker block, each skill file, the install dir and the data dir. It never touches anything it didn't create. For package-manager channels it prints the manager's uninstall command instead.

## Failure modes

| situation | behavior |
|---|---|
| checksum mismatch | Stop, delete the download, exit nonzero, print the expected and actual hash. |
| marker block edited by hand | Replace it anyway, keep a `.bak` of the file first. |
| receipt missing | `doctor` warns. `uninstall` refuses and prints manual steps. |
| agent config file missing | Skip that agent, report it, continue. |
| running inside a package-manager install | `update` and `uninstall` defer to the manager. |
