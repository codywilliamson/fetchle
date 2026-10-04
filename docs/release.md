# Release pipeline

Reference for CI, versioning and release artifacts.

## Versioning

release-please with `release-type: simple`. Conventional commits drive the version and changelog. An `extra-files` entry uses the xml updater to rewrite the `<Version>` element in `Directory.Build.props`, so the assembly version tracks the release.

```json
{
  "packages": {
    ".": {
      "release-type": "simple",
      "extra-files": [{ "type": "xml", "path": "Directory.Build.props", "xpath": "//Project/PropertyGroup/Version" }]
    }
  }
}
```

## Workflows

| workflow | trigger | does |
|---|---|---|
| `ci.yml` | push to any branch, PRs from forks | Build, unit, e2e and evals on windows-latest, ubuntu-latest, macos-latest. |
| `bench.yml` | push to main, release | BenchmarkDotNet plus the rg comparison, results uploaded as an artifact. Not yet: comparing to committed JSON and failing past a threshold. The committed baseline comes from a dev machine, so comparing it to a shared runner would be noise; that needs a runner-made baseline first. |
| `release-please.yml` | push to main | Opens or updates the release PR. |
| `publish.yml` | release created | NativeAOT matrix, archives, checksums, provenance, upload. Not yet: the NuGet tool package. |
| `demos.yml` | release published | Re-render VHS tapes, attach GIFs. See [demos.md](demos.md). |

A release made with `GITHUB_TOKEN` doesn't trigger other workflows, so `release-please.yml` calls `publish.yml` and `bench.yml` as reusable workflows when it creates a release. `publish.yml` also runs on a manually published release and on `workflow_dispatch` with a tag.

## Native build matrix

NativeAOT can't cross-compile across operating systems, so each OS builds its own binaries.

| RID | runner |
|---|---|
| win-x64 | windows-latest |
| win-arm64 | windows-latest |
| linux-x64 | ubuntu-latest |
| linux-arm64 | ubuntu-24.04-arm |
| osx-arm64 | macos-latest |

Local Windows builds need the VS Build Tools C++ workload and `vswhere.exe` on PATH. Without the latter, link fails with `'vswhere.exe' is not recognized` baked into the linker command. Hosted runners have both.

## Release artifacts

Per RID: `fetchle-<version>-<rid>.zip` (Windows) or `.tar.gz` (Unix) containing the exe and the int8 model, plus `.sha256`. Until embeddings ship, the archive holds only the exe.

Provenance is signed with `actions/attest-build-provenance`, so users can verify with `gh attestation verify`.

The .NET tool package ships the same native binaries as a platform-specific tool.

## Dependencies

Renovate, grouped weekly, automerge for patch updates that pass CI.

Every project except `Fetchle.Cli` commits a `packages.lock.json`, and CI restores with `--locked-mode`. `setup-dotnet` caches the NuGet folder keyed on those lock files. `Fetchle.Cli` has no lock file because `PublishAot` adds the host's ILCompiler runtime package to its restore graph, so its lock file would differ on every OS. Its third-party packages are still locked through `Fetchle.Tests`, which references it. `build.cs` has no lock file for the same reason.

The SDK adds implicit packages such as `Microsoft.NET.ILLink.Tasks` at its bundled runtime version. When the 10.0 SDK on the runners moves to a new patch, locked restore fails with NU1004 until the lock files are regenerated with `dotnet build.cs restore` on that SDK.

`dotnet build.cs publish --rid <rid>` restores the library projects for that RID too, which adds a RID section to their lock files in the working tree. That's fine on release runners. Locally, discard those lock file changes before the next locked restore.
