# Contributing to fetchle

fetchle is pre-alpha, so most work is building out the [roadmap](docs/roadmap.md). Read [docs/architecture.md](docs/architecture.md) before changing code, and check [docs/decisions.md](docs/decisions.md) before reopening a settled choice.

## What you need

- The .NET SDK version pinned in `global.json`.
- A native toolchain for the NativeAOT build:
  - Windows: Visual Studio Build Tools with the "Desktop development with C++" workload. `build.cs` puts `vswhere` on PATH for you.
  - Linux: `clang` and `zlib1g-dev`.
  - macOS: the Xcode command line tools (`xcode-select --install`).

Without the native toolchain, the JIT build and unit tests still work, but publish and e2e fail at the link step.

## The loop

Every build step goes through one C# file-based app:

```sh
dotnet build.cs build
dotnet build.cs test
dotnet build.cs e2e
dotnet build.cs eval
dotnet build.cs bench
```

`test` runs the unit tests. `e2e` publishes the native exe and drives it against fixture trees. `eval` scores ranking quality against a committed baseline. `bench` runs BenchmarkDotNet and the rg comparison. `dotnet build.cs ci` runs the whole pipeline once.

A change isn't done until the native exe passes e2e. The JIT build can pass while the AOT build is broken, because trimming and source generators fail in ways the JIT never sees. [docs/testing.md](docs/testing.md) explains what each suite covers.

## Conventions

Commits are conventional and lowercase, one concern per commit, like `fix(cli): clamp --since cutoffs older than DateTimeOffset.MinValue`. release-please reads them to pick the version and write the changelog, so the type matters.

The build enforces `.editorconfig`, so a style violation fails the build like any other warning.

Write a comment only when the why isn't obvious from the code. Keep it lowercase and informal.

A change to a hot path (walk, index, tokenize, embed, rank, output) carries BenchmarkDotNet numbers from before and after, with `[MemoryDiagnoser]` on, in the commit body.

When you settle a design question, add an entry to [docs/decisions.md](docs/decisions.md) with the evidence that settled it.

The specs in [docs/specs/](docs/specs/) are the contract the e2e suite tests. When behavior and a spec disagree, fix one of them in the same change.

## Pull requests

Open a PR against `main`. CI builds and runs unit tests, e2e and evals on Windows, Linux and macOS, and all three must be green before merge. Benchmarks run on main after merge, not per PR, because shared runners are too noisy for perf gates.
