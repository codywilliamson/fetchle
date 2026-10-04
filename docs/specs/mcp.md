# MCP spec

`fetchle mcp` runs a stdio MCP server on `ModelContextProtocol.Core`, with hand-written handlers and no hosting. The pattern comes from [spikes/mcp.md](../spikes/mcp.md).

## Tools

### `find_files`

Ranked search over the index.

| arg | type | default | meaning |
|---|---|---|---|
| `query` | string | required | Plain-words description or partial name. |
| `limit` | int | 10 | Max results. |
| `budget_ms` | int | 2000 | Hard time cap. Clamped to 25000 to stay under client transport timeouts. |
| `root` | string | all roots | Restrict to one indexed root. |
| `ext` | string[] | none | Extension filter. |

Returns text content (one path per line plus the footer) and `structuredContent`:

```json
{
  "paths": [{ "path": "C:\\Users\\Cody\\AppData\\Roaming\\Claude\\claude-code\\2.1.286\\635c1867224a\\claude.exe", "score": 0.81, "size": 222298112, "modified": "2026-10-01T14:02:11Z" }],
  "total_matches": 2,
  "shown": 2,
  "elapsed_ms": 6,
  "stopped_early": null
}
```

`stopped_early` is `null`, `"budget"` or `"limit"`. When the index is still building, results come from what's indexed so far and the footer says which roots are incomplete.

### `index_status`

No args. Returns roots, file counts, last full scan time, whether vectors are complete, and whether the watcher is live. Gives an agent a way to decide whether to trust a miss.

## Errors

Unknown tools and bad arguments throw `McpProtocolException` with `McpErrorCode.InvalidParams`, which reaches the client as JSON-RPC error -32602. A plain `McpException` would come back as an `isError: true` tool result instead, which agents treat as a search failure.

## Schema compatibility

Keep tool names and argument names stable once v0.3 ships. Agents and skills get written against them.
