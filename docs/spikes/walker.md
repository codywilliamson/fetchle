# Walker spike findings

Code and data in this folder: `src\Program.cs`, `src\Walker.csproj`; NativeAOT build `bin\Walker.exe` (1.6 MB), JIT build `bin-jit\`; scripts in `scripts\` (`gci-diag.ps1`, `gci-perdir.ps1`, `gci-ab.ps1`, `gci-missing-root.ps1`, `gci-missing-demo.ps1`, `gci-orig.ps1`, `bench.ps1`); raw results `scripts\results-three.jsonl`, `scripts\results-profile.jsonl`, `scripts\gci-diag-pwsh7.txt`.

## Why gci timed out (verified)

Not gci's walk speed. One of the three roots, `$env:LOCALAPPDATA\AnthropicClaude`, doesn't exist on this machine. When `-Path` points at a missing leaf with `-Recurse`, PowerShell doesn't fail. It treats the leaf name as a name to search for and crawls the whole parent. The agent's command crawled all of `%LOCALAPPDATA%`.

- **Minimal repro, same on pwsh 7.6.6 and 5.1.26100.** `gci-missing-demo.ps1` builds a temp tree containing `a\b\AnthropicClaude`. `gci -Path <tmp>\AnthropicClaude -Recurse` returns `\a\b\AnthropicClaude`. `-LiteralPath` returns nothing.
- **What it crawled.** `%LOCALAPPDATA%` holds 2,485,082 entries. Raw .NET took 114 s to enumerate it.
- **Missing root alone, pwsh 7:**

| variant | result |
|---|---|
| `gci -Path <missing> -Recurse -Filter claude*.exe` | timed out after >240 s |
| same with `-LiteralPath` | 126 ms, one "Cannot find path" error |
| without `-Recurse` | 130 ms |

- **Original command** (180 s timeout; 2 matches, 0 errors when it finished):

| command | pwsh 7 | ps 5.1 |
|---|---|---|
| all 3 roots | timed out after 180 s | timed out after 180 s |
| only the 2 roots that exist | 3.1 s | 4.3 s |

- `Select-Object -First 5` can't stop early: only 2 files match.
- **Junctions are not the cause.** 36 junctions under Packages point at `E:\WpSystem` (Xbox games), ~6,800 entries. pwsh 7 and 5.1 both list 85,494 entries (walker count + ~36 junction entries), so neither descends. `cmd dir /s` does follow them: 92,258.
- **Access denied is not the cause.** `-ErrorVariable` was 0 on the existing roots.
- **`-Filter` vs `-Include`** on Packages: `-Filter` 4.0 s, `-LiteralPath -Filter` 3.4 s, no filter 4.6 s, `-Include` 70.2 s. `-Include` is ~17x slower but isn't what hit the timeout. Why it's slow is not verified.

With valid roots, gci is still ~12x slower than the AOT parallel walker (6.0 s vs 0.5 s) with ~9x the memory (186 MB vs 21 MB).

Agent fixes: `Test-Path` roots first or use `-LiteralPath`; never `-Include` with `-Recurse`.

## Results

AOT publish worked after VS BuildTools install, with `${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer` on PATH.

Machine: Windows 11, 16 logical CPUs, NVMe and SATA SSDs. Every tool ran as a child process; peak MB is the child's peak working set.

Variants: **a** `Directory.EnumerateFileSystemEntries`; **b** `FileSystemEnumerable` with a transform that only allocates strings for matches; **c** parallel per-directory work queue, N threads. All skip reparse points, ignore inaccessible, include hidden/system. "list" prints every path; "search" prints only `claude*.exe` matches.

### (i) Roots that exist: `APPDATA\Claude` + `LOCALAPPDATA\Packages`

Cache already warm. Median of runs 2-4.

| tool | ms | entries | entries/s | peak MB |
|---|---|---|---|---|
| aot-a list | 1,299 | 85,456 | 66k | 19.8 |
| aot-b list | 1,238 | 85,456 | 69k | 19.8 |
| aot-c4 list | 777 | 85,456 | 110k | 20.4 |
| aot-c8 list | 602 | 85,456 | 142k | 20.7 |
| aot-c16 list | 506 | 85,456 | 169k | 21.3 |
| aot-a search | 1,245 | 85,456 | 69k | 19.5 |
| aot-b search | 1,232 | 85,456 | 69k | 13.4 |
| aot-c16 search | 515 | 85,456 | 166k | 16.7 |
| jit-b search | 1,303 | 85,456 | 66k | 30.9 |
| jit-c16 search | 574 | 85,456 | 149k | 33.6 |
| `rg --files --hidden --no-ignore` | 345 | 77,014 (files only) | 223k | 17.3 |
| `cmd /c dir /s /b /a` | 3,794 | 92,258 (follows junctions) | 24k | 9.4 |
| gci pwsh7 `-Recurse -Force` | 6,146 | 85,494 | 14k | 186 |
| gci ps5.1 `-Recurse -Force` | 7,303 | 85,494 | 12k | 137 |
| gci pwsh7, original minus missing root | 3,506 | 2 matches | n/a | 100 |
| gci, original with missing root (both shells) | timed out 180 s | n/a | n/a | n/a |

### (ii) All of `C:\Users\Cody`: ~7.69M entries, 6.68M files

| tool | runs (ms) | entries/s, warm | peak MB |
|---|---|---|---|
| aot-b list (first full walk, cold, 1 run) | 334,474 | 23k | 85 |
| aot-b search | 218,913 / 188,175 / 149,455 | 41k-51k | 87-90 |
| aot-c16 list | 80,354 (cold-ish) / 60,432 / 59,741 | 128k | 81-97 |
| aot-c16 search | 60,448 / 59,790 / 59,251 | 129k | 95-101 |
| rg --files | 42,270 / 44,570 / 47,594 | 140k-158k files/s | 17.4 |
| cmd dir /s /b | not measured, killed after >10 min | n/a | n/a |
| gci pwsh7 and 5.1 | not measured, cut at wrap-up | n/a | n/a |

Also not measured: fd (not installed), aot-a/c4/c8 on the full profile, JIT on the full profile.

## Which variant wins (verified)

- **Parallel c16.** 0.51 s vs 1.23 s for a/b on the 3 roots (2.4x). 60 s vs ~150-190 s warm for b on the full profile (2.5-3x). 4 → 8 → 16 threads keeps helping.
- **b vs a:** no speed difference. b saves memory only when matches are few (13.4 vs 19.5 MB).
- **AOT vs JIT:** same walk speed. AOT halves memory (17 vs 34 MB), startup ~60-270 ms (noisy).
- **ripgrep still wins.** ~1.5x faster on the 3 roots (and lists 10% fewer entries, it skips dirs), ~1.3x on the full profile, 17 MB vs our 100 MB. Single-threaded we're faster (`rg -j1` 1.6 s vs our b 1.2 s). rg scales 4.6x at 16 threads, we scale ~2.5x.

## Recommended approach

1. NativeAOT parallel walker on non-recursive `FileSystemEnumerable` with a transform; allocate strings only for matches and queued directories.
2. Replace the global FIFO queue + semaphore with per-worker LIFO deques and work stealing (as rg's ignore crate does). Guess: a breadth-first queue holds the whole directory frontier in memory, explaining the 100 MB, and rg's scaling suggests contention or poor locality in ours.
3. Skip reparse points by default, don't follow junctions, tolerate access denied silently.
4. A missing root is an immediate error. Never glob it into a search of the parent.
5. For agents, stopping early at N matches helps a lot when matches exist. No matches still costs a full walk.

## Verified vs guessed

Verified: missing-root recursion caused the timeout (timings + minimal repro, both shells); junctions and access denied are not the cause; `-Include` ~17x slower than `-Filter`; every number in the tables; AOT publish works.

Guessed: why `-Include` is slow; why our parallel walker scales worse than rg; that work-stealing DFS closes the speed and memory gap; that walker startup noise is Defender. `EnumerationOptions.BufferSize` at 0/16K/64K showed no effect (measured roughly).
