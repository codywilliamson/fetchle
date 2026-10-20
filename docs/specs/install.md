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
4. Extract the exe into the install dir. The model file joins it once embeddings ship; v0.1 has none.
5. Run `fetchle setup --yes`, which handles PATH and the receipt (agent registration lands in v0.3).

Re-running the script upgrades or repairs. It never adds a second PATH entry.

The scripts read their settings from env vars, because `irm | iex` can't take arguments.

| variable | meaning |
|---|---|
| `FETCHLE_VERSION` | Release to install, `0.1.0` or `v0.1.0`. Default: the latest GitHub release. |
| `FETCHLE_DOWNLOAD_BASE` | Where `fetchle-<version>-<rid>.zip` (`.tar.gz` on Unix) and its `.sha256` live: a URL or a local directory. Needs `FETCHLE_VERSION`. CI installs from a locally built archive this way. |
| `FETCHLE_INSTALL_DIR` | Install dir override. |

Supported platforms are win-x64, win-arm64, linux-x64, linux-arm64 and osx-arm64. Anything else fails with a message naming them. `scripts/archive.sh` makes the archives for both `publish.yml` and the install CI job, so the layout the scripts unpack can't drift from what releases ship.

| OS | install dir | data dir (index, savings) |
|---|---|---|
| Windows | `%LOCALAPPDATA%\fetchle\bin` | `%LOCALAPPDATA%\fetchle` |
| Linux | `~/.local/share/fetchle/bin` | `$XDG_DATA_HOME/fetchle`, else `~/.local/share/fetchle` |
| macOS | `~/.local/share/fetchle/bin` | `~/Library/Application Support/fetchle` |

`FETCHLE_DATA_DIR` and `FETCHLE_INSTALL_DIR` override both, for the binary and the scripts alike. Tests and CI use them (and a temp `HOME` or `LOCALAPPDATA`) so a run never touches the real install.

## The receipt

`<data dir>/receipt.json`, written by `setup`, read by `update`, `doctor`, `cleanup` and `uninstall`.

```json
{
  "version": "0.1.0",
  "channel": "script",
  "install_dir": "C:\\Users\\Cody\\AppData\\Local\\fetchle\\bin",
  "data_dir": "C:\\Users\\Cody\\AppData\\Local\\fetchle",
  "path_edit": { "kind": "windows-user-path", "entry": "C:\\Users\\Cody\\AppData\\Local\\fetchle\\bin" },
  "agents": [
    { "agent": "claude-code", "kind": "mcp", "file": "C:\\Users\\Cody\\.claude.json" },
    { "agent": "claude-code", "kind": "skill", "file": "C:\\Users\\Cody\\.claude\\skills\\fetchle\\SKILL.md" }
  ]
}
```

On Unix, `path_edit` is `{ "kind": "shell-rc-block", "entry": "<install dir>", "file": "<rc file>" }`. In v0.1 `agents` is always `[]`.

## `setup`

v0.1 does PATH and the receipt only; the agent registration below is v0.3 on the roadmap.

- Windows: adds the install dir to the front of `HKCU\Environment` `Path`, keeping the value's registry kind (new values are `REG_EXPAND_SZ`) and other entries' `%VARS%` unexpanded. An existing copy is kept once and duplicates are dropped. It then broadcasts `WM_SETTINGCHANGE` so new terminals see it.
- Unix: writes a marker block into one rc file, chosen from `$SHELL`: zsh `~/.zshrc`, bash `~/.bashrc`, anything else `~/.profile`. The block is a guarded `export PATH=...` that is safe to source twice. A re-run replaces the block in place; if the existing block differs from what setup would write (hand-edited), the file is copied to `<rc>.bak` first. Duplicate blocks collapse to one.
- If a previous receipt used a different rc file, that old block is removed.

v0.3 will detect Claude Code, Codex, Cursor, VS Code, OpenCode and Gemini CLI, show a checklist, then for each selected agent register the MCP server and install a skill that tells the agent to call fetchle instead of `gci` or `find`.

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

Removes exactly what the receipt lists: the PATH entry, each marker block, each skill file, the install dir and the data dir. It never touches anything it didn't create. For package-manager channels it prints the manager's uninstall command (`brew uninstall fetchle`, `winget uninstall fetchle`, `scoop uninstall fetchle`, `dotnet tool uninstall -g fetchle`) and exits 0 without removing anything.

It lists the steps and asks to continue; `--yes` skips the question, and without a terminal it refuses unless `--yes` is given. A receipt whose directories are a filesystem root, the home dir or a relative path is refused. An rc file that held only the fetchle block is deleted. A parent directory named `fetchle` is removed when it ends up empty.

Windows can't delete a running exe. Uninstall deletes everything it can, then starts a detached `cmd` that retries removing what's left for about a minute, which outlasts the process exiting. The command exits 0 once that is started. Unix deletes everything directly.

## Failure modes

| situation | behavior |
|---|---|
| checksum mismatch | Stop, delete the download, exit nonzero, print the expected and actual hash. |
| marker block edited by hand | Replace it anyway, keep a `.bak` of the file first. |
| receipt missing | `doctor` warns. `uninstall` refuses, prints manual steps and exits 5. |
| agent config file missing | Skip that agent, report it, continue. |
| running inside a package-manager install | `update` and `uninstall` defer to the manager. |
