# Hype video storyboard

A concept preview, about 32 seconds at 1920x1080, 30 fps. It sells the product we're building, so the in-product numbers (6 ms, 4 ms) are the targets from [vision.md](../../docs/vision.md), not measurements. The pain numbers (120 s timeout, 2,485,082 entries, 5.6 MB exe) are real, from the spikes. The outro says "concept preview" so nobody mistakes it for a benchmark.

Paths on screen use `~` and fixture names. No real home directory.

| # | scene | length | beat |
|---|---|---|---|
| 1 | Cold open | 5 s | An agent's `gci -Recurse` crawls. File counter races to 2,485,082, the timer to 120 s, the screen reddens, then `timed out · 0 results`. |
| 2 | The guess | 2.5 s | Black. "It guessed a folder." "It guessed wrong." (red, shake) |
| 3 | Reveal | 3 s | `fetchle` letters spring in, a light streak flies through, a ring pulses. "Describe the file. Get the path." |
| 4 | Search | 5 s | `fetchle "desktop app bundled claude binary"` types, ranked results slide in with score bars, footer pops `6 ms`. |
| 5 | Race | 3.5 s | Same question. gci's bar crawls to a red 120 s; fetchle's bar snaps full at 6 ms. |
| 6 | Meaning | 4 s | "music streaming app" flies through a field of files and lands on `Spotify.exe`. "Zero shared keywords." |
| 7 | Agents | 4 s | An MCP `find_files` call types out, ranked paths stream back inside the budget. |
| 8 | Stats | 4 s | Cards slam in: 5.6 MB native exe, 0 bytes leave your machine, Windows/Linux/macOS, .NET 10 NativeAOT. |
| 9 | Outro | 4 s | Wordmark, fetchle.dev, "open source · coming soon", "concept preview". |

Look: near-black, one hot orange accent for fetchle, red for the old way, cyan for success. Space Grotesk headlines, JetBrains Mono for anything that's a terminal. Slow drifting glow and a scrolling grid underneath everything so it never feels static.

Render:

```bash
pnpm --dir demo/video install
pnpm --dir demo/video render
```

The soundtrack is synthesized by `scripts/soundtrack.mjs`, scored to the same frame timeline: a drone and accelerating ticks under the crawl, impacts on the timeout and "It guessed wrong.", a drop on the reveal, then a four-on-the-floor beat with UI blips on every on-screen event. No samples, so no licensing. `pnpm render` regenerates it first. If you move a scene, update `SCENE` in the script.
