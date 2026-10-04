# FINDINGS: XenoAtom terminal stack

**Verdict: use with caveats.** NativeAOT publishes with zero warnings and the exe is 5.2 MB. OSC 8 links and synchronized output (DEC 2026) work out of the box, and redirected stdout comes out as plain text. Caveats: ~30 ms startup cost on some Windows hosts, live widgets leak spinner frames into redirected output, no built-in taskbar progress, and a non-obvious focus rule in the picker.

Versions: XenoAtom.Terminal 2.2.0, XenoAtom.Terminal.UI 3.10.0, XenoAtom.CommandLine 2.0.3, XenoAtom.Ansi 1.7.1. .NET SDK 10.0.100, Windows 11.

Code: Program.cs, FakeData.cs, Indexer.cs, Picker.cs, SelfTest.cs. Escape captures in `captures\`, build logs in publish-aot.log and publish-trim.log.

## AOT and trimming

- `dotnet publish -c Release -r win-x64 -p:PublishAot=true`: zero IL2xxx/IL3xxx warnings.
- Trimmed self-contained publish with `TrimmerSingleWarn=false`: zero warnings.
- Link needed `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` on PATH (otherwise `'vswhere.exe' is not recognized`).

## Size and startup

Median of 15 runs, stdout to `$null`. Noisy machine, first run often 2-5x slower.

| Build | Size | `--help` | Print 20 results |
|---|---|---|---|
| NativeAOT | 5.2 MB | 43 ms | 82 ms (45 ms with init fix) |
| Hello-world AOT (baseline) | 1.1 MB | 38 ms | n/a |
| Trimmed + ReadyToRun single file | 22 MB | 107 ms | 175 ms |
| Hello-world trimmed + R2R | 16 MB | 95 ms | n/a |

- First terminal call costs ~30 ms on Windows when neither `WT_SESSION` nor `TERM_PROGRAM` is set. That's `DetectTerminalName()` inspecting console-host and parent processes. Setting `TERM_PROGRAM` drops init to 0.2-0.3 ms and the results run from 82 ms to 45 ms. Windows Terminal sets `WT_SESSION`, so users there don't pay it; conhost, CI and piped runs do. Worth an upstream issue (lazy check, or skip when redirected).
- In a real terminal (AOT): init 2 ms, 20 lines written in 29 ms, 63 ms end to end. Batching into one write barely changed it.
- `--help` never touches the terminal layer, so it runs at baseline speed.

## Features

- **OSC 8 hyperlinks: native.** `AnsiWriter.BeginLink(url)` / `EndLink()`. Captured: `\e]8;;file:///C:/repo/docs/ranker_12.json\e\docs/\e[33m\e[1mrank\e[0mer_12.json\e]8;;\e\  \e[2m   1.6 MB  47d ago\e[0m`. No markup tag for links, only the writer API. Terminal.UI also has a `Link` control.
- **DEC 2026 synchronized output: native.** Every live frame wrapped in `\e[?2026h … \e[?2026l` (198 pairs in the progress capture). Off when redirected.
- **NO_COLOR: respected, color only.** Yellow highlight gone; bold, dim, links and cursor control stay. Matches the no-color.org spec. `ForceAnsi` overrides it.
- **Redirected stdout: plain text**, no escapes at all, OSC 9;4 included. With a known CI var (e.g. `GITHUB_ACTIONS=true`) it switches to a CI mode that keeps bold/dim/color but drops links and private modes.
  - **Bug in our use:** `Terminal.Live` still runs when redirected and writes spinner frames as junk (`⠋⠙⠹⠸⠼⠴⠦⠧…` on one line before results). Skip live widgets ourselves when `Capabilities.IsOutputRedirected` is true.
  - `ForceAnsi` plus redirect: the live region thinks it's 1 column wide and renders only the spinner.
- **OSC 9;4 taskbar progress: not built in.** Emitted via `w.WriteOsc(9, "4;1;{pct}")`, correctly dropped when ANSI is off. Writing it during `Live` counts as output, so the region is erased and redrawn every percent change. Rate-limit it or ask upstream for a side channel.
- **`--help` from XenoAtom.CommandLine** is clean GNU-style: `Usage: fwip [options] [<query>]`, aligned options, `-h, -?, --help` / `-v, --version` built in. Optional `XenoAtom.CommandLine.Terminal` for colored help, not tried.

## Verified vs not verified

Verified:
- AOT publish, size, zero warnings.
- Startup numbers above.
- Results print in a real terminal pane with links and dim metadata.
- Progress bar renders at full width in a real terminal pane.
- AOT escape stream for the results print is byte-identical to JIT (hash match).
- Picker end to end on `InMemoryTerminalBackend` (full capabilities, injected keys): types `rnkr`, Down twice, Enter; returns the expected third fuzzy match (`src/ranker_659.csproj`) on JIT and AOT. Match count updated 5000 → 4315 → 2104 → … per keystroke; escape stream shows 2026-wrapped diff frames.

Not verified:
- A human typing into the picker in a real console (key timing, IME, resize).
- Inline region appearance and flicker in Windows Terminal or conhost.
- Whether OSC 9;4 actually moves the taskbar.
- Linux and macOS.

## API notes and rough edges

- **Picker focus gotcha:** inside `Terminal.Live`, typed text didn't reach the `TextBox` until it was focused explicitly with `ctx.App.Focus(input)` from the update callback. Without it every keystroke is silently dropped. Not in the docs.
- Bind Up/Down/Enter/Esc with `AddCommand` + `KeyGesture`; commands run before the focused control sees the key. No preview key event.
- No built-in fuzzy/filterable prompt. `SelectionPrompt` is a fixed list, so the picker is a hand-built `VStack(ListBox, TextBlock, TextBox)`, ~80 lines.
- Docs are good where they exist; XML docs ship in the packages. Focus, NO_COLOR rules and CI detection required reading source.
- Terminal.UI.dll is 4 MB of IL with a source generator, needs C# 14 / net10.0. AOT trims to the 5.2 MB exe.
- Fluent, consistent API; pleasant once focus was solved.
