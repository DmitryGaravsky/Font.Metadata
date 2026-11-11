# Testing

The current suite checks the implemented foundation, not font parsing.

## Available checks

| Check | Coverage |
| --- | --- |
| Library NUnit tests | Public namespace, assembly identity and nested type accessibility |
| CLI NUnit tests | Help aliases, version output, unsupported inspection and invalid arguments |
| Library package check | net10.0 assembly, XML documentation and README; no other framework assets or CLI payload |
| Package licensing | Both packages declare MIT, preserve author/copyright and project/repository metadata, and include the repository LICENSE text |
| Isolated consumer | Compile and execute against the generated library nupkg |
| Release scripts | Valid/invalid tags, checksum failures, draft recovery, immutable asset collisions and no-op retries using a local GitHub CLI substitute |
| Installed tool | Install the generated nupkg with a fresh package cache; verify help and version |

CLI tests execute a child dotnet process with redirected stdout/stderr and a timeout. They verify the real executable output rather than replacing the console in-process.

## Full verification

```powershell
pwsh -File eng/verify.ps1
```

An optional `-Version 1.2.3-preview.1` overrides the development version consistently for restore, build and pack. Packages are written to `artifacts/packages/<version>/`.

Outputs are written under artifacts: TRX reports, nupkg files and isolated package-smoke runs. Each smoke run uses a unique directory and cache to avoid reusing a stale package with the same development version.

## Focused verification

After a successful build:

```shell
dotnet test Font.Metadata.Tests/Font.Metadata.Tests.csproj -c Release --no-build --no-restore
dotnet test Font.Metadata.Tool.Tests/Font.Metadata.Tool.Tests.csproj -c Release --no-build --no-restore
dotnet format whitespace Font.Metadata.slnx --verify-no-changes --no-restore
```

Rebuild before using --no-build after source/configuration changes. Both test projects run on net10.0 on every supported host.

## Release preparation without publication

```powershell
pwsh -File eng/test-release.ps1
pwsh -File eng/prepare-release.ps1 -Tag v1.2.3-preview.1
```

Preparation runs the full verification, builds a portable ZIP from the checked tool package, extracts it into a fresh directory and checks help/version output and the included license text. It writes two nupkg files, the ZIP and SHA256SUMS under `artifacts/release/1.2.3-preview.1/`. It requires no Git tag, GitHub credentials or NuGet publication credentials and never publishes. The release workflow runs this on all three operating systems.

The GitHub publication script is tested with a local command substitute; these checks do not contact GitHub. Actual OIDC authentication, NuGet push and hosted release creation are verified only during an explicitly initiated release.

## Adding functionality

Parser changes require independent valid/malformed fixtures, bounds, resource-limit and cancellation checks. Partial-result changes must assert preserved values, table states, original indexes and diagnostics. License-analysis changes require source evidence, incomplete-input and conflict tests.

Do not claim these future behaviors are covered by the foundation tests. Keep public docs and test coverage aligned with implemented behavior.
