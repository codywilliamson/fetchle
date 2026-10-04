# FINDINGS: Model2Vec.Net for ranked path search

## Verdict

Model2Vec.Net is usable: on NuGet, pure managed, matches Python output, publishes NativeAOT with zero IL/AOT warnings, AOT process starts in ~60-110 ms with the model loaded.

The ranking plan needs one change. Lexical top-300 then embedding rerank can't rescue what lexical missed; lexical missed the target in 5 of 12 queries, so the planned pipeline missed it too. Embeddings must be a second candidate source, fused with lexical by RRF. Depth penalty and default pruning (node_modules, Temp, cache dirs) matter about as much as model choice.

Recommended model: **potion-retrieval-32M int8** (32 MB on disk). Fallback: potion-base-8M int8 (7.5 MB) if binary size wins. potion-code-16M doesn't load in this library.

## Verified results

**Library**
- Model2Vec.Net 0.1.3 on NuGet (net10.0). Deps: Microsoft.ML.Tokenizers 2.0, System.Numerics.Tensors, M.E.AI.Abstractions.
- Only loader is `Model2VecModel.Load(path)`, needs `model.safetensors`, `tokenizer.json`, `config.json` on disk. No stream/byte overload, so embedding the model in the exe means extracting to temp first.
- Embeddings held as `float[]`. int8 shrinks download and load spike, not steady-state RAM.
- Matches Python model2vec to 5 decimals (potion-retrieval-32M).
- **potion-code-16M fails**: `KeyNotFoundException: '[SEP]'` in `Microsoft.ML.Tokenizers.BertTokenizer` (no [SEP]/[CLS] in its WordPiece vocab). Python loads it fine, so it's a library limitation. Vocab-quantization path untested as a result.

**Model sizes**

| model | f32 | f16 | int8 | tokenizer.json |
|---|---|---|---|---|
| potion-base-8M | 30.2 MB | 15.1 MB | 7.6 MB | 0.68 MB |
| potion-retrieval-32M | 129.2 MB | 64.6 MB | 32.3 MB | 1.49 MB |
| potion-code-16M | 64.3 MB | 32.6 MB | 16.8 MB | 1.04 MB |

- HF doesn't publish int8; made with Python model2vec (`from_pretrained(quantize_to="int8")` + `save_pretrained`).
- int8 loads in .NET; library ignores the scale, harmless since a global scale cancels under normalize.
- int8 vs f32 cosine over 20k real segments: mean 0.9998 (8M), 0.9995 (32M), min 0.989. f16 identical to f32. Rankings within a few positions.

**NativeAOT (win-x64)**
- 0 IL/AOT warnings. Needed VS Installer dir on PATH for `vswhere`.

| build | exe size |
|---|---|
| model on disk | 5.56 MB |
| base-8M int8 embedded | 13.6 MB |
| retrieval-32M int8 embedded | 38.9 MB |

Cold start (process, load + first encode, warm disk cache, 5 runs; embedded timings after first run, which extracts in 155-228 ms):

| build | process | model load | working set |
|---|---|---|---|
| AOT base-8M int8 | 61-80 ms | 22 ms | 57 MB |
| AOT base-8M f32 | 68-74 ms | 28-32 ms | 79 MB |
| AOT retrieval-32M int8 | 110-132 ms | 68-79 ms | 188 MB |
| AOT retrieval-32M f32 | 129-159 ms | 85-104 ms | 281 MB |
| AOT embedded base-8M int8 | 63-67 ms | 23-25 ms | 57 MB |
| AOT embedded retrieval-32M int8 | 103-123 ms | 63-75 ms | 188 MB |
| JIT retrieval-32M int8 | 292-451 ms | ~160 ms | 208 MB |

First encode: 0.05 ms AOT, ~50 ms JIT.

**Corpus: `C:\Users\Cody\AppData`** (reparse points skipped, inaccessible ignored)
- Full: 2,625,318 files, 907k unique segments, 26.6M total. Enumeration 279-362 s.
- Pruned (node_modules, .git, Temp, any dir containing "cache"): 567,584 files, 441,746 unique segments, 4.28M total. Enumeration 37 s.
- ~10x segment dedup in the pruned set.
- Embedding all unique segments, single-threaded AOT: base-8M 10.0-12.6 s (~23-29 µs/segment), retrieval-32M 16.5-17.9 s (~37-41 µs/segment). JIT similar.
- f32 segment-vector cache: 431 MB (256-dim) / 862 MB (512-dim). Peak working set 1.9 / 2.4 GB.
- Per query over 567k paths: centroid scoring 33-74 ms, plain BM25 145-420 ms (unoptimized).

**Scorers**
- Lexical: BM25 over split words (camelCase, separators, digits), filename tokens counted twice.
- Centroid: cosine vs weighted mean of segment vectors (filename 2, parent 1, other ancestors 0.5).
- Coverage: each query word takes its best segment, averaged.
- +depth: centroid minus 0.02 per segment.
- RRF k=60. Variants: lex300 reranked by centroid (original plan); lex300 + centroid300 as two sources.

**Rank of expected target (pruned, 567k paths, "-" = not found)**

| query | lexical | 8M cen+depth | 32M centroid | 32M cen+depth | 32M coverage | 32M lex300→rerank | 32M lex300+cen300 (depth) |
|---|---|---|---|---|---|---|---|
| desktop app bundled claude binary | 2097 | 641 | 24 | 7 | 8978 | - | 23 |
| powershell command history | 1092 | 22 | 116 | 94 | 28477 | - | 170 |
| vscode user settings | 23 | 1 | 348 | 295 | 376 | 49 | 30 |
| windows terminal config | 7 | 151931 | 35920 | 7911 | 2292 | 32 | 17 |
| chrome browsing history | 189 | 23 | 54 | 58 | 1 | 119 | 101 |
| firefox bookmarks database | 260 | 141 | 21 | 19 | 17 | 45 | 38 |
| node version manager settings | 1382 | 656 | 4153 | 612 | 2312 | - | - |
| github desktop executable | 259 | 2 | 3 | 3 | 513 | 37 | 21 |
| discord app executable | 26 | 1 | 1 | 1 | 153 | 3 | 1 |
| obsidian vault list | 31 | 1 | 3 | 3 | 118 | 12 | 5 |
| thunderbird mail search index | 140 | 49 | 984 | 288 | 285 | 123 | 129 |
| music streaming app | - | 132 | 1 | 1 | 8622 | - | 26 |

Unpruned 2.6M corpus was much worse: base-8M lexical put claude.exe at 16290, centroid at 5648, flooded by node_modules and Temp scratchpad copies.

**Top-5 examples (retrieval-32M int8)**
- "music streaming app": lexical gives FiveM streaming_surrogate.rpf, gta-streaming-five.dll, Spotify ...collection-music-download.css. Centroid+depth gives `Roaming\Spotify\Spotify.exe`, SpotifyStartupTask.exe, save-to-spotify.exe, SpotifyLauncher.exe, spotify_cli.exe.
- "desktop app bundled claude binary": lexical gives claude_desktop_config.json, claude-desktop.bash/.fish/.zsh (target 2097). Centroid+depth gives claude_desktop_config.json, GitHubDesktop desktop-notifications.node ... (target `claude-code\<ver>\<hash>\claude.exe` at 7).
- "windows terminal config" (embeddings fail): lexical gives `Local\Microsoft\Windows Terminal\Fragments\...`, WindowsTerminal_8wekyb3d8bbwe\Settings\roaming.lock (target 7). Centroid gives MiKTeX config.deskjet, avidemux config3, Claude git-shadow\<hash>\config. `Microsoft.WindowsTerminal_8wekyb3d8bbwe` splits into "microsoft windows terminal 8 wekyb 3 d 8 bbwe" and the junk dilutes the vector.

## What worked
- On NuGet, AOT-clean, Python-exact. Model load 20-75 ms AOT, single encode ~0.05 ms.
- Embeddings bridge vocabulary gaps: "music streaming app" → Spotify.exe (#1), "binary" → claude.exe, "bookmarks database" → places.sqlite, "executable" → *.exe.
- Weighted centroid with filename boost plus depth penalty was the best embedding scorer.
- retrieval-32M beats base-8M on descriptive queries (claude, firefox, spotify); base-8M won vscode/powershell.
- int8: same quality, ~4x smaller.
- Per-segment caching pays off at ~10x dedup.

## What didn't work
- **Lexical-candidates-then-rerank failed on 5 of 12 queries** (target never in lexical top 300).
- Coverage scorer was bad: generic words ("app", "history", "config") match junk.
- Hash, GUID and package-family segments poison embeddings. Filter or down-weight before embedding.
- Without default pruning both scorers drown (2.6M paths, 6-minute enumeration).
- Plain BM25 is a weak baseline; real lexical needs prefix/fuzzy, depth/recency prior, ignore rules.
- potion-code-16M doesn't load.
- Embedding all 442k segments up front: 10-18 s single-threaded plus 431-862 MB f32. Too slow and big per invocation.

## Recommendation
- **potion-retrieval-32M int8** loaded from disk (5.6 MB exe + 32 MB model, ~110 ms cold start). Embedded gives a 39 MB exe at the same speed but still needs a temp extract. base-8M int8 fallback (13.6 MB embedded, ~65 ms).
- Pipeline: prune by default, lexical top-N and embedding top-N as separate candidate sources, fuse with RRF, apply a depth prior. Embedding side queries a persisted segment-vector index, not just lexical's candidates.
- Store segment vectors as int8 or f16 (verified cosine ≥0.989), not f32.

## Guesses / not verified
- Slow per-segment encode is probably the ML.Tokenizers tokenizer, not vector math. Not profiled. `Model2VecModel` is documented thread-safe, so parallel encode should scale; not measured.
- Persisted int8 index for 442k×512 ≈ 216 MB (arithmetic, not measured).
- Target regexes are strict, so some ranks undercount (e.g. `Local\GitHubDesktop\GitHubDesktop.exe` was #1 for the GitHub query but didn't match).
- 12 queries on one machine is anecdotal. Depth penalty and segment weights untuned.
- Cold start assumes a warm OS file cache.

## Files
- App: `Spike\Program.cs`, `Corpus.cs`, `Lexical.cs`, `Semantic.cs`, `Spike.csproj` (`-p:Aot=true`, `-p:EmbedModel=<dir>`)
- Queries: `queries.tsv`; raw output `results-*.txt` (AOT: `results-aot-*.txt`)
- Exes: `publish\disk`, `publish\embed8`, `publish\embed32`
- Python: `tools\quantize.py`, `tools\parity.py`
- Library source: `src-ref\`
- ~600 MB disposable: `models\`, `paths-all.txt` (355 MB), `paths-pruned.txt` (69 MB)
