# Continuous integration

[The workflow](../.github/workflows/ci.yml) runs on pushes to `main`, pull requests and manual dispatch. Pull requests verify Ubuntu and Windows. Pushes to `main` and manual runs also verify macOS. Feature-branch pushes do not start a separate run, avoiding duplicate checks for an open pull request. Branches without a pull request can be checked through manual dispatch. Version tag pushes use the separate [release workflow](../.github/workflows/release.yml).

Each job installs the latest stable .NET 10 SDK (10.0.x), then runs the same [verification script](../eng/verify.ps1) used locally:

```text
Restore ──→ Release build ──→ NUnit tests ──→ whitespace check
                                                    │
                                                    ▼
Local nupkg packing ──→ isolated consumer ──→ tool installation
                                                    │
                                                    ▼
                                     Upload reports and packages
```

The reader, adapter, tool and three test projects use net10.0 on every runner. Each job has a timeout and failures in one operating system do not cancel the others.

TRX reports are uploaded even when a later check fails. Local development packages are uploaded from the successful Linux job. Temporary caches and installed tools are not uploaded.

The CI workflow requires only repository read permission and does not use publication credentials. The separate tag-triggered release workflow verifies the exact tagged commit on all three operating systems, then publishes the verified Linux artifacts through two dependent jobs: NuGet first, GitHub Release second. See [Releases](release.md) for triggers, authentication and recovery.

Hosted results must be established from a retained workflow run for the relevant revision. Local verification and the presence of a workflow do not establish that a hosted runner job has passed.

## Documentation checks

Package/tool verification checks the documented CLI JSON examples against the local schema. It does not automatically establish that all Markdown links, C# snippets, capability descriptions or prose contracts are current. Those require the [documentation review](../CONTRIBUTING.md#formatting-and-documentation). Repository-wide link/snippet and API-documentation drift checks are not currently implemented; they are separate CI work, not a guarantee provided by this workflow.

Workflow action usage follows the maintainers' documentation: [checkout](https://github.com/actions/checkout), [setup-dotnet](https://github.com/actions/setup-dotnet), [upload-artifact](https://github.com/actions/upload-artifact).

## Published SPDX dependency

Local, build/test CI and tag-release verification restore the exact Spdx.Expressions 0.1.0 package from nuget.org through the repository configuration. No additional checkout or producer build is required. Package acceptance uses the restored artifact in a closed local feed with fresh consumer caches and checks DLL identity, notices, installed-tool behavior and the portable payload. See [dependency verification](spdx.md#build-and-verification).
