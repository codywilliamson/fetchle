# Competitors and building blocks

Surveyed 2026-10-03. Star counts were taken then.

## Direct competitors

| project | stack | how it indexes | ranking | platforms | notes |
|---|---|---|---|---|---|
| [fastfind](https://github.com/adisingh396/fastfind) | Rust, MCP | NTFS `$MFT` via `FSCTL_ENUM_USN_DATA` (admin), parallel walk fallback; resident in RAM, ~21 s for 1.8M files | none, attribute filters only | Windows | Closest to fetchle. No live updates yet (roadmap). Has a 25 s capped `grep` tool. 1 star, MIT. |
| [elis132/everything-mcp](https://github.com/elis132/everything-mcp) | Python, MCP | voidtools Everything via `es.exe` | Everything's sort | Windows | Requires Everything installed and running. 23 stars, MIT. |
| [EverythingMCP](https://github.com/DanMarshall909/EverythingMCP) | MCP | Everything SDK, HTTP fallback | Everything's sort | Windows | Same dependency. |
| [mcp-everything-search](https://mcpservers.org/servers/mamertofabian/mcp-everything-search) | MCP | Everything / `mdfind` / `plocate` | backend's | Win, mac, Linux | Shells out to whatever the OS has. |
| [Argus](https://github.com/0x4Devs/argus) | C++20, Qt6 | MFT + USN live updates | fuzzy | Windows | GUI plus `argus-cli.exe`. Admin required. No MCP. |

## Adjacent: content search

| project | what it does |
|---|---|
| [semble](https://github.com/MinishLab/semble) | Code chunk search for agents. Model2Vec + BM25 + RRF. The model fetchle follows. |
| [ken](https://github.com/townsendmerino/ken) | Go port of semble with drop-in MCP schema compatibility. |
| [semble-rs](https://github.com/mrorigo/semble-rs) | Rust port of semble. |
| [LocalSynapse](https://localsynapse.com/en/blog/mcp-local-file-search) | Document content search, BM25 + semantic, offline. |
| [FileIndexr](https://github.com/art-den/file_indexr) | Local text search server with MCP. |

## Building blocks

| project | use in fetchle | caveat |
|---|---|---|
| [Model2Vec.Net](https://github.com/ericstj/Model2Vec.Net) | Embedding inference. Pure C#, AOT-clean, matches Python. | Loads from a directory only. `potion-code-16M` fails to load. See [spikes/embeddings.md](spikes/embeddings.md). |
| [XenoAtom.Terminal / Terminal.UI / CommandLine](https://xenoatom.github.io/) | Terminal rendering, live picker, arg parsing. | Startup and redirect caveats in [spikes/terminal.md](spikes/terminal.md). |
| [ModelContextProtocol.Core](https://github.com/modelcontextprotocol/csharp-sdk) | MCP stdio server. | Use Core without hosting. See [spikes/mcp.md](spikes/mcp.md). |
| [MFTLib](https://github.com/mtschoen/MFTLib) | Possible MFT + USN backend. | Native C++ core, admin or broker process. Not evaluated. |

## Where fetchle is different

Nobody ranks. Everything wrappers depend on a closed source app. fastfind and Argus need admin for their fast path. None of them is .NET, and none is a fd-style finder written in C#.
