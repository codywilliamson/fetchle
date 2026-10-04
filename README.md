# fetchle

Describe a file in plain words and fetchle brings back the path, ranked, in milliseconds. It's built for AI agents and humans, runs as one NativeAOT .NET binary, and nothing leaves your machine.

```text
> fetchle "desktop app bundled claude binary"
  AppData/Roaming/Claude/claude-code/2.1.286/635c1867224a/claude.exe   212 MB  2d ago
  AppData/Roaming/Claude/claude-code/2.1.284/3f4bed3e44ad/claude.exe   211 MB  9d ago
  2 of 2 matches · 6 ms
```

That output is the target, not shipped behavior. fetchle is pre-alpha. Search works end to end, but it's a naive placeholder: one thread walks the tree and matches your query as a case-insensitive substring of the path, inside a time budget. The real walker, the index, ranking and the file watcher are the next work. Most other commands exist but print "not implemented yet". See the [roadmap](docs/roadmap.md) for what lands when.

## Why it exists

An agent looking for one exe ran `Get-ChildItem -Recurse` over three AppData folders and hit a 120 second timeout with nothing to show. One folder didn't exist, and PowerShell quietly turned that into a crawl of all 2.5 million entries under `%LOCALAPPDATA%`. The same search in raw .NET found the exe in 1.2 seconds. Agents improvise filesystem crawls like that constantly, and they can't tell which roots are expensive. fetchle gives them one call that answers from a local index, inside a time budget, every time.

[semble](https://github.com/MinishLab/semble) does this for code content. fetchle does it for file names and paths.

## Try it

You need the .NET SDK pinned in `global.json`, plus a native toolchain for the AOT build. [CONTRIBUTING.md](CONTRIBUTING.md) lists what each OS needs.

Build the native exe:

```sh
dotnet build.cs publish
```

It lands in `artifacts/publish/`. Search the current directory, or point it at another root:

```sh
artifacts/publish/fetchle config
artifacts/publish/fetchle config --root ~/src
```

A root that doesn't exist fails right away with exit code 2 instead of turning into a slow crawl.

Run the MCP server over stdio, for an agent to connect to:

```sh
artifacts/publish/fetchle mcp
```

It exposes `find_files` and `index_status`. The [MCP spec](docs/specs/mcp.md) has the tool shapes.

## Docs

- [Vision and goals](docs/vision.md)
- [How it works](docs/architecture.md)
- [CLI spec](docs/specs/cli.md), [MCP spec](docs/specs/mcp.md), [install spec](docs/specs/install.md)
- [Contributing](CONTRIBUTING.md), [testing](docs/testing.md), [release pipeline](docs/release.md), [demo videos](docs/demos.md)
- [Roadmap](docs/roadmap.md), [decisions](docs/decisions.md), [competitors](docs/competitors.md)
- [Spike results](docs/spikes/)

## License

MIT
