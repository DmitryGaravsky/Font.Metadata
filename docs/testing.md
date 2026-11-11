# Testing

The suite exercises the public SFNT/TTC reading API, partial recovery, resource limits, license evidence, packaging and current CLI behavior.

## Available checks

| Check | Coverage |
| --- | --- |
| Public contract | Namespace, assembly identity, nested model accessibility and immutable results, including empty collections for unavailable metadata |
| SFNT | Supported flavors; exact case-sensitive signatures; independent binary vector; preferred names, all source IDs, duplicates, empty values, culture independence |
| Name decoding | Formats 0/1, language references, UTF-16 errors, MacRoman, Windows CJK, unsupported encodings |
| OS/2 | Vendor and raw fsType; truncated prefixes and unsupported versions; recovery independent of name data |
| TTC | Versions 1/2, absolute offsets, failed face placeholders, stable indexes, shared tables and per-face diagnostics |
| Directories | Bounds, integer extremes, duplicate tags, overlaps, unsorted entries and independent table recovery |
| Limits | Input, table, face, directory, record, per-table/global string and diagnostic budgets; failed decode accounting |
| Input contracts | Arrays/slices/span/memory, file ownership, short reads, non-seekable streams, position restoration, I/O and cancellation |
| Recovery regressions | Unsupported sibling headers, unreadable language references, preferred-value fallback and raw evidence preservation |
| Allocation regressions | Long repeated records and shared preferred values, with generous managed-allocation ceilings rather than timing thresholds |
| Cancellation during parsing | Deterministic cancellation during preferred-name selection through internal test access, without timing races |
| Robustness | Every truncation of representative inputs and deterministic seeded mutations through the public API |
| License rules | Every supported ID/name/family and URL; exact context, negations, quotes, versions, compound expressions and URI spoofing |
| License contract | Original-record identity, duplicates, conflicts, all name statuses, immutable results, cultures and concurrent calls |
| License robustness | Deep/long rejected inputs without recursion; shared strings with a managed-allocation ceiling; shared TTC source records |
| CLI | Help aliases, version, unsupported inspection and invalid arguments through a child process |
| Library package | net10.0 assembly, XML documentation and README; no other framework assets or CLI payload |
| Package licensing | MIT declaration, author/copyright, project/repository metadata and bundled LICENSE |
| Isolated consumer | Compile and execute real strict/partial reads and complete/partial license analysis against the generated library nupkg, including source identity and immutable hints |
| Release scripts | Tag validation, checksum failures, draft recovery, asset collisions and no-op retries with a local GitHub CLI substitute |
| Installed tool | Fresh local package cache/tool path; help and version |

## Fixture provenance and design

Font fixtures are synthetic byte sequences authored in this repository and covered by its MIT license. No proprietary fonts or third-party font binaries are redistributed. FontFixtures uses independent byte-writing helpers; at least one fixed hexadecimal vector exercises reading without relying on the fixture builder. Legacy-encoding vectors contain explicit source bytes.

Fixtures focus on observable contracts. Recovery tests assert preserved values, original indexes, table statuses and diagnostic codes rather than exception message text. Faulting and short-read streams verify ownership and propagation. Culture tests restore their original culture. Mutation checks are deterministic regression probes, not a claim of exhaustive fuzzing or full OpenType conformance.

## Full verification

```powershell
pwsh -File eng/verify.ps1
```

This runs offline release-script checks, restore, Release build, both NUnit projects, whitespace verification, package creation and isolated package/tool checks. An optional `-Version 1.2.3-preview.1` overrides the development version consistently for restore, build and pack.

Outputs are ignored under artifacts: TRX reports, nupkg files and isolated package-smoke runs. Each smoke run uses a unique directory and cache to avoid stale packages with the same version. The package consumer restores from a local-only feed.

## Focused verification

```shell
dotnet test Font.Metadata.Tests/Font.Metadata.Tests.csproj -c Release
dotnet test Font.Metadata.Tool.Tests/Font.Metadata.Tool.Tests.csproj -c Release
dotnet format whitespace Font.Metadata.slnx --verify-no-changes --no-restore
```

Use --no-build only after a current successful build, and --no-restore only with current assets. Both test projects target net10.0. The configured CI matrix runs verification on Windows, Linux and macOS; a local run only verifies its own host.

## Release preparation without publication

```powershell
pwsh -File eng/prepare-release.ps1 -Tag v1.2.3-preview.1
```

Preparation includes the full verification, builds a portable ZIP from the checked tool package, extracts it into a fresh directory and checks help/version output and license text. It writes two nupkg files, the ZIP and SHA256SUMS under `artifacts/release/1.2.3-preview.1/`. It creates no Git tag, requires no publication credentials and never publishes.

The GitHub publication script is tested with a local command substitute; these checks do not contact GitHub. Actual OIDC authentication, NuGet push and hosted release creation are verified only during an explicitly initiated release.

## Extending coverage

Parser changes require independent valid/malformed fixtures, bounds, resource-limit and cancellation checks. Keep existing recovery contracts while adding container support. WOFF/WOFF2/EOT decoding, embedded search and CLI reports are not implemented or covered as successful operations yet.

License matching tests use independent expected strings and synthetic name tables, rather than enumerating production dictionaries. Contract tests exercise reader-produced complete/partial faces and all section states; internal construction is reserved for isolated status and stress cases that cannot be represented within name wire limits. Boundary regressions cover continued titles, quoted semicolons, standalone SPDX annotation lines and their rejected continuations. Long-input tests do not use fragile timing assertions. Shared-text allocation tests warm the implementation before measuring with a generous ceiling.

Changes to [license rules](licensing.md) require positive and negative examples, exact RuleId/identifier/kind assertions and original SourceRecord identity. Do not convert arbitrary prose, newer versions or compound expressions into positive cases without a deliberate contract change. Package smoke tests compile every new public type and verify actual evidence through the generated nupkg.
