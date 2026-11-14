# Testing

The suite exercises the public SFNT/TTC/WOFF/WOFF2/EOT reading API, partial recovery, resource limits, license evidence, packaging and current CLI behavior.

## Verification layers

| Layer | Purpose | Evidence and scope |
| --- | --- | --- |
| Repository tests | Check public contracts, parser recovery, limits and CLI behavior with controlled fixtures | Test results apply to the tested revision and host; the CI matrix determines hosted platform coverage |
| Package and portable checks | Exercise actual nupkg consumers, the installed tool and the extracted portable CLI | Verify packaging and documented behavior without assuming a ProjectReference build represents the distributed artifact |
| Independent package and corpus validation | Evaluate real-world metadata, evidence and reproducible differences through the public API of an identified package | Initial research cycle completed with documented limits; further checks run for release candidates or relevant changes, separately from repository CI |

A successful repository pipeline does not establish completion of the independent validation campaign. Likewise, an observed baseline records a package's behavior; it does not turn that behavior into a correctness expectation.

## Independent validation

The initial independent validation cycle concluded on 2026-11-13 with documented limitations, on Windows with .NET SDK 10.0.402. The evaluated Font.Metadata 0.1.0-dev package has SHA-256 `7C5079568C533AEB1F5F0392ADFD0A0982BEC197DBD4E2CEA938D9E748D7F825`. All 135 Inspect/Read/Analyze checks passed against reviewed expectations for 45 samples, including CJK, variable TrueType/CFF2, COLRv1/SVG/sbix, shared collections and partial-recovery inputs. No product defect was confirmed on this corpus; initial expectation errors were corrected after contract review with the original evidence retained.

The separate harness preserves source records and evidence, performs field-level package comparisons, and supports replay from saved inputs and run manifests. At research closeout, 89 passing tests validated the harness itself. FontTools 4.68.0 comparisons produced 27 agreements and 18 inconclusive cases, with no differences in comparable metadata; all five container-equivalence checks passed. OTS 9.2.0 results remain separate: rejection of a metadata-only fixture is not automatically a reader defect.

FontTools also contributed to real-font expectation preparation, so its agreement is not an additional oracle independent of those expectations. That adapter profile does not read EOT or WOFF2 collections; their reviewed cases and container checks must not be described as FontTools agreement. The 45 samples include derived containers and synthetic cases, not 45 independent real-world font families.

A subsequent Windows robustness campaign evaluated the same package through 62 reviewed contract/boundary profiles and 256 deterministic mutations from eight seeds. All 318 worker executions completed with an enforced 512 MiB Windows Job committed-memory cap and a four-second timeout per case. The reviewed profiles covered Inspect/Read stream and overload contracts, all 14 reader limits, shared strings, zlib/Brotli expansion, independently encoded MTX/LZCOMP/RLE and aggregate main/EUDC budgets. Mutation results establish only that no execution failure was observed, not correctness of the extracted metadata. Controlled processes separately verified memory-limit notifications, tree termination and failure minimization/reproduction; no product failure required minimization.

An exploratory Windows performance study used BenchmarkDotNet 0.15.8 on eight reviewed inputs, separating Inspect/Read over preloaded bytes, Analyze over decoded metadata, combined reading/analysis and file/stream I/O. Three baseline and two candidate runs compared two distinct builds of 0.1.0-dev (baseline SHA-256 above; candidate SHA-256 `94DBE7954F886FF0257688A0276B040C2B6F06B011DE19EC6B319B129A7AE2E6`), not two published versions; parallel and sequential corpus runs were measured separately. Eleven of 48 microbenchmark cases remained inconclusive because of temporal drift. A small timing signal was inconclusive on repetition; a repeated parallel-batch allocation signal includes scheduler and harness overhead and remains under review. No product performance regression was confirmed, and no release timing threshold was established.

Managed allocated bytes per operation, macro batch allocations and process peak working set are different measurements; none establishes an exact parser peak-memory bound. Byte-input parsing excludes file loading, whereas file/stream paths include buffering. Cold-cache/startup and long-duration workloads were not measured.

Completed validation reports should identify the package version and SHA-256, corpus revision and sample provenance, adapter/tool versions, options and limits, environment, and unresolved findings. Reports should distinguish reviewed expectations, observations from independent tools and baseline observations from Font.Metadata. Findings need reproducible inputs or recipes; missing required evidence must not be reported as a successful check.

This completed cycle provides an initial regression baseline, not representative coverage of real-world fonts or release approval. Positive FLA002/FLA003 cases, selected negative/conflicting examples and hint provenance are covered; broad license-recognition precision/recall remains unmeasured. Long-running fuzzing, cross-platform performance and stable timing thresholds also remain open. No confirmed product finding from this bounded cycle requires an immediate code change. Release candidates and parser, decoder, limit or license-rule changes require their own scoped validation; no ongoing campaign or mandatory performance gate is implied. See [Processing untrusted fonts](security.md) for existing protections and [Preview readiness](release.md#preview-readiness) for the review before publication.

## Recording and reusing results

A report records what ran against an identified artifact; it does not certify the current working tree or another package with the same version. Keep these fields with each validation record:

| Field | Required distinction |
| --- | --- |
| Artifact | Package ID/version/SHA-256, source revision when known, and any source changes; do not guess an unavailable revision. |
| Inputs and configuration | Corpus/suite identity and hashes, provenance/licenses, reviewed expectation revision, options, limits and seeds. |
| Environment | OS/architecture, SDK/runtime and adapter/tool versions; benchmark host and measurement boundary when relevant. |
| Execution | Run ID/date, commands, checks actually executed, counts with their units, skipped/unavailable checks and original reports. |
| Interpretation | Expected contract versus observation; agreement, disagreement and inconclusive results; unresolved finding IDs and coverage gaps. |
| Follow-up | Fix/recheck identity, retained original evidence and the event that requires a new run. |

Keep corpus checks, harness tests, worker completion and benchmark cases as separate counts. Seeded input mutation is not mutation testing of the implementation, coverage-guided fuzzing or proof that extracted metadata is correct. A missing oracle or unavailable platform remains unverified rather than passing.

Corrections to validation tools or expectations require a dated explanation and new observations; retain the original hashed reports. Checks of a test launcher or its SDK environment do not establish a new product correctness or performance baseline. Keep their scope separate from checks of the library itself.

Recheck a release candidate as a new artifact. Parser/decoder/limit changes trigger the affected corpus and robustness checks; license-rule changes trigger reviewed evidence comparisons. Performance work first needs same-package controls and a stable host baseline. A new platform/runtime needs its own environment record. These are event-driven follow-ups, not an active recurring research campaign.

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
dotnet test Font.Metadata.Tool.Tests/Font.Metadata.Tool.Tests.csproj -c Release --no-restore
dotnet test Font.Metadata.Spdx.Tests/Font.Metadata.Spdx.Tests.csproj -c Release --no-restore
dotnet format whitespace Font.Metadata.slnx --verify-no-changes --no-restore
```

Use --no-build only after a current successful build, and --no-restore only with current assets. Both test projects target net10.0. The configured CI matrix runs verification on Windows and Linux for pull requests, and also on macOS for pushes to main, manual runs and releases; a local run only verifies its own host.

## Release preparation without publication

```powershell
pwsh -File eng/prepare-release.ps1 -Tag v1.2.3-preview.1
```

Preparation includes the full verification, which builds the portable ZIP from the tool package, extracts it into a fresh directory and checks help/version, real plain/JSON inspection, schema, recovery and license text. Release preparation copies that same verified ZIP without rebuilding it. It writes three nupkg files, the ZIP and SHA256SUMS under `artifacts/release/1.2.3-preview.1/`. It creates no Git tag, requires no publication credentials and never publishes.

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

## SPDX integration checks

Adapter tests use independently built font records and expected expressions to check recognition boundaries, provenance, duplicate/shared records, UTF-16 coordinates, all six native AST node kinds, exact and one-over parser limits, per-face budgets and immutable results. CLI tests cover independent flags, legacy report snapshots, syntax/parser failures, declaration limits, validation truncation, source completeness, escaping and cancellation during output. Package verification uses a fresh closed feed, checks the actual transitive DLL and notices, and runs both installed and portable commands against the extended JSON schema, including invalid/empty expressions, explicit and bare parser limits, known/unknown/wrong-kind identifiers, qualified references and truncated validation. These integration checks do not retroactively extend the historical corpus findings above. Hosted Windows/Linux/macOS verification remains a separate executed gate.
