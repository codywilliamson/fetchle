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
| `ci.yml` | PR, push to main | Build, unit, e2e and evals on windows-latest, ubuntu-latest, macos-latest. |
| `bench.yml` | push to main, release | BenchmarkDotNet, compare to committed JSON, fail past threshold. |
| `release-please.yml` | push to main | Opens or updates the release PR. |
| `publish.yml` | release published | NativeAOT matrix, archives, checksums, provenance, upload, NuGet tool package. |
| `demos.yml` | release published | Re-render VHS tapes, attach GIFs. See [demos.md](demos.md). |

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

Per RID: `fetchle-<version>-<rid>.zip` (Windows) or `.tar.gz` (Unix) containing the exe and the int8 model, plus `.sha256`.

Provenance is signed with `actions/attest-build-provenance`, so users can verify with `gh attestation verify`.

The .NET tool package ships the same native binaries as a platform-specific tool.

## Dependencies

Renovate, grouped weekly, automerge for patch updates that pass CI.
