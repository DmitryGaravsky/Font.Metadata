# Continuous integration

[The workflow](../.github/workflows/ci.yml) runs on branch pushes, pull requests and manual dispatch. Version tag pushes use the separate [release workflow](../.github/workflows/release.yml). It uses GitHub-hosted Ubuntu, Windows and macOS runners.

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

The library, tool and both test projects use net10.0 on every runner. Each job has a timeout and failures in one operating system do not cancel the others.

TRX reports are uploaded even when a later check fails. Local development packages are uploaded from the successful Linux job. Temporary caches and installed tools are not uploaded.

The CI workflow requires only repository read permission and does not use publication credentials. The separate tag-triggered release workflow verifies the exact tagged commit on all three operating systems, then publishes the verified Linux artifacts through two dependent jobs: NuGet first, GitHub Release second. See [Releases](release.md) for triggers, authentication and recovery.

The first hosted run becomes possible after the repository is uploaded to GitHub with Actions enabled. Local verification does not establish that a hosted runner job has passed.

Workflow action usage follows the maintainers' documentation: [checkout](https://github.com/actions/checkout), [setup-dotnet](https://github.com/actions/setup-dotnet), [upload-artifact](https://github.com/actions/upload-artifact).
