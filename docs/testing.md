# Testing

The suite exercises the public SFNT/TTC/WOFF/WOFF2/EOT reading API, partial recovery, resource limits, license evidence, packaging and current CLI behavior.

## Verification layers

| Layer | Purpose | Evidence and scope |
| --- | --- | --- |
| Repository tests | Check public contracts, parser recovery, limits and CLI behavior with controlled fixtures | Test results apply to the tested revision and host; the CI matrix determines hosted platform coverage |
| Package and portable checks | Exercise actual nupkg consumers, the installed tool and the extracted portable CLI | Verify packaging and documented behavior without assuming a ProjectReference build represents the distributed artifact |
| Independent package and corpus validation | Evaluate real-world metadata, evidence and reproducible differences through the public API of an identified package | Initial bounded corpus validation completed; broader coverage remains in progress, separately from repository CI |

A successful repository pipeline does not establish completion of the independent validation campaign. Likewise, an observed baseline records a package's behavior; it does not turn that behavior into a correctness expectation.

## Independent validation

Independent validation was expanded on 2026-11-13 on Windows with .NET SDK 10.0.402. The evaluated Font.Metadata 0.1.0-dev package has SHA-256 `7C5079568C533AEB1F5F0392ADFD0A0982BEC197DBD4E2CEA938D9E748D7F825`. All 135 Inspect/Read/Analyze checks passed against reviewed expectations for 45 samples, including CJK, variable TrueType/CFF2, COLRv1/SVG/sbix, shared collections and partial-recovery inputs. No product defect was confirmed on this corpus; initial expectation errors were corrected after contract review with the original evidence retained.

The separate harness preserves source records and evidence, performs field-level package comparisons, and supports replay from saved inputs and run manifests. Its 65 passing tests validate the harness itself. FontTools 4.68.0 comparisons produced 27 agreements and 18 inconclusive cases, with no differences in comparable metadata; all five container-equivalence checks passed. OTS 9.2.0 results remain separate: rejection of a metadata-only fixture is not automatically a reader defect. Detailed observations and reproducibility artifacts remain in the separate research workspace.

Completed validation reports should identify the package version and SHA-256, corpus revision and sample provenance, adapter/tool versions, options and limits, environment, and unresolved findings. Reports should distinguish reviewed expectations, observations from independent tools and baseline observations from Font.Metadata. Findings need reproducible inputs or recipes; missing required evidence must not be reported as a successful check.

This is an initial regression baseline, not representative coverage of real-world fonts. Positive FLA002/FLA003 cases are now covered, but broad license-recognition accuracy, hostile-input resilience and parsing performance remain unestablished. Broader validation and review of the actual release candidate remain necessary. See [Processing untrusted fonts](security.md) for existing protections and [Preview readiness](release.md#preview-readiness) for the review before publication.

## Available checks

| Check | Coverage |
| --- | --- |
| Public contract | Namespace, assembly identity, nested model accessibility and immutable results, including empty collections for unavailable metadata |
| SFNT | Supported flavors; exact case-sensitive signatures; independent binary vector; preferred names, all source IDs, duplicates, empty values, culture independence |
| Name decoding | Formats 0/1, language references, UTF-16 errors, MacRoman, Windows CJK, unsupported encodings |
| OS/2 | Vendor and raw fsType; truncated prefixes and unsupported versions; recovery independent of name data |
| TTC | Versions 1/2, absolute offsets, failed face placeholders, stable indexes, shared tables and per-face diagnostics |
| WOFF | Raw/zlib combinations, exact output/trailer/checksum/member consumption, independent recovery and optional block layout |
| WOFF2 | Single/collection, shared tables, integer encodings, transforms, discarded output, partial Brotli recovery and bounded descriptor traversal |
| EOT/MTX | Versioned header facts, raw/XOR/LZCOMP/RLE, main/EUDC independence, root checksum/signature framing and all-stream budgets |
| Source coordinates | Logical table offsets and physical anchors across raw/compressed main/EUDC data, slices and positioned streams |
| Directories | Bounds, integer extremes, duplicate tags, overlaps, unsorted entries and independent table recovery |
| Limits | Input, table, face, directory, record, per-table/global string and diagnostic budgets; failed decode accounting |
| Input contracts | Arrays/slices/span/memory, file ownership, short reads, non-seekable streams, position restoration, I/O and cancellation |
| Recovery regressions | Unsupported sibling headers, unreadable language references, preferred-value fallback and raw evidence preservation |
| Allocation regressions | Long repeated records and shared preferred values, with generous managed-allocation ceilings rather than timing thresholds |
| Cancellation during parsing | Deterministic cancellation during preferred-name selection, directory/header traversal, XOR, Brotli, LZ copy and RLE through internal test access, without timers |
| Robustness | Every truncation of representative inputs and deterministic seeded mutations through the public API |
| License rules | Every supported ID/name/family and URL; exact context, negations, quotes, versions, compound expressions and URI spoofing |
| License contract | Original-record identity, duplicates, conflicts, all name statuses, immutable results, cultures and concurrent calls |
| License robustness | Deep/long rejected inputs without recursion; shared strings with a managed-allocation ceiling; shared TTC source records |
| CLI | Real-process inspection, stdin, plain/JSON, recovery, evidence and argument errors; deterministic cancellation/output failures |
| Library package | net10.0 assembly, XML documentation and README; no other framework assets or CLI payload |
| Package licensing | MIT declaration, author/copyright, project/repository metadata and bundled LICENSE |
| Isolated consumer | Compile and execute real strict/partial reads and complete/partial license analysis against the generated library nupkg, including source identity, immutable hints, web formats, EOT fields/provenance, logical diagnostic offsets and new limits; zlib host strict-validation settings run in separate processes |
| Release scripts | Tag validation, checksum failures, draft recovery, asset collisions and no-op retries with a local GitHub CLI substitute |
| Installed tool | Fresh local package cache/tool path; help/version, real plain/JSON inspection, schema, recovery and license evidence |

## Fixture provenance and design

Font fixtures are synthetic byte sequences authored in this repository and covered by its MIT license. No proprietary fonts or third-party font binaries are redistributed. FontFixtures uses independent byte-writing helpers; at least one fixed hexadecimal vector exercises reading without relying on the fixture builder. Legacy-encoding vectors contain explicit source bytes.

WebFontFixtures independently writes container layouts and uses BCL compression for WOFF/Brotli. EotMtxFixtures contains synthetic Windows TTEmbed output with and without RLE. EotFontVectors contains independent Google sfntly output, with the pinned source revision and input descriptions recorded alongside the vectors. The [long-copy recipe](../Font.Metadata.Tests/TestData/mtx-copy-loop.md) documents reproducibility. No encoder, Windows font API, Java or network access is required during tests.

Fixtures focus on observable contracts. Recovery tests assert preserved values, original indexes, table statuses and diagnostic codes rather than exception message text. Faulting and short-read streams verify ownership and propagation. Culture tests restore their original culture. Mutation checks are deterministic regression probes, not a claim of exhaustive fuzzing or full OpenType conformance.

The web-container review regressions cover inflated compressed-length declarations, optional-block overlap versus independent failure, final WOFF padding, RootString work accounting (including invalid UTF-16), resize cancellation, copy-chunk boundaries and preservation of unsupported payload flavors.

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

Use --no-build only after a current successful build, and --no-restore only with current assets. Both test projects target net10.0. The configured CI matrix runs verification on Windows and Linux for pull requests, and also on macOS for pushes to main, manual runs and releases; a local run only verifies its own host.

## Release preparation without publication

```powershell
pwsh -File eng/prepare-release.ps1 -Tag v1.2.3-preview.1
```

Preparation includes the full verification, builds a portable ZIP from the checked tool package, extracts it into a fresh directory and checks help/version, real plain/JSON inspection, schema, recovery and license text. It writes two nupkg files, the ZIP and SHA256SUMS under `artifacts/release/1.2.3-preview.1/`. It creates no Git tag, requires no publication credentials and never publishes.

The GitHub publication script is tested with a local command substitute; these checks do not contact GitHub. Actual OIDC authentication, NuGet push and hosted release creation are verified only during an explicitly initiated release.

## Extending coverage

Parser changes require independent valid/malformed fixtures, bounds, resource-limit and cancellation checks. Keep existing recovery contracts while adding container support. CLI reports are covered through real processes and internal orchestration tests. Embedded search remains deferred.

License matching tests use independent expected strings and synthetic name tables, rather than enumerating production dictionaries. Contract tests exercise reader-produced complete/partial faces and all section states; internal construction is reserved for isolated status and stress cases that cannot be represented within name wire limits. Boundary regressions cover continued titles, quoted semicolons, standalone SPDX annotation lines and their rejected continuations. Long-input tests do not use fragile timing assertions. Shared-text allocation tests warm the implementation before measuring with a generous ceiling.

Changes to [license rules](licensing.md) require positive and negative examples, exact RuleId/identifier/kind assertions and original SourceRecord identity. Do not convert arbitrary prose, newer versions or compound expressions into positive cases without a deliberate contract change. Package smoke tests compile every new public type and verify actual evidence through the generated nupkg.

## CLI contract checks

Process tests cover all formats, argument validation before I/O, binary stdin, original face/record indexes, EOT recovery, optional source-linked license evidence, invariant JSON and terminal-safe text. Internal host tests deterministically cancel waiting asynchronous pipe reads and large report writes, separate input/output failures, and exercise incremental flushing. A fixed plain snapshot protects readable layout. Review regressions close a real child-process output pipe, cancel help/version output, check short reads and oversized stdin producers, and verify argument/cancellation/I/O precedence. The process timeout covers input delivery as well as output collection and process exit.

The shared eng/verify-cli.ps1 smoke check runs against both installed and extracted portable tools through eng/test-cli-verification.ps1. It validates every option combination and complete/partial/I/O reports against docs/cli.schema.json, validates documented examples, and rejects deliberately invalid schema examples. Validation is local and requires no downloaded schema or font.

Console Ctrl+C signal delivery is host-dependent and is not simulated by killing a child process. Local cancellation tests use a real asynchronous named pipe and the same command token path. They do not establish immediate cancellation of all native stdin/file/output operations. CI verifies cross-platform execution, with macOS covered on main, manual runs and releases; a local Windows run does not prove Linux/macOS behavior.

The verification-script regression launches a separate PowerShell process with the GitHub Actions preamble, dot-sourced `-Command` invocation and `exit $LASTEXITCODE` footer. A successful smoke test must return zero and resume its caller despite expected CLI codes 1 and 3. An unexpected CLI failure and an invalid JSON example in an isolated fixture must still fail the shell. The smoke script explicitly exits zero only after all assertions and cleanup; exceptions are not suppressed. Per-case stdout/stderr remain in the package/portable smoke artifacts.
