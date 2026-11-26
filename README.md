# Font.Metadata

[![Build and test (main)](https://github.com/DmitryGaravsky/Font.Metadata/actions/workflows/ci.yml/badge.svg?branch=main&event=push)](https://github.com/DmitryGaravsky/Font.Metadata/actions/workflows/ci.yml?query=branch%3Amain+event%3Apush)
[![License](https://img.shields.io/github/license/DmitryGaravsky/Font.Metadata)](https://github.com/DmitryGaravsky/Font.Metadata/blob/main/LICENSE)
[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4)](https://github.com/DmitryGaravsky/Font.Metadata/blob/main/Font.Metadata/Font.Metadata.csproj)

[![Font.Metadata on NuGet](https://img.shields.io/nuget/v/Font.Metadata?label=Font.Metadata)](https://www.nuget.org/packages/Font.Metadata/)
[![Font.Metadata.Spdx on NuGet](https://img.shields.io/nuget/v/Font.Metadata.Spdx?label=Font.Metadata.Spdx)](https://www.nuget.org/packages/Font.Metadata.Spdx/)
[![Font.Metadata.Tool on NuGet](https://img.shields.io/nuget/v/Font.Metadata.Tool?label=Font.Metadata.Tool)](https://www.nuget.org/packages/Font.Metadata.Tool/)

Font.Metadata is an MIT-licensed .NET 10 library and command-line tool for font inventories and license evidence inspection.

Read names, attribution and declared license information from **TrueType/OpenType, TTC/OTC, WOFF, WOFF2 and EOT** files without installing fonts or loading a rendering engine. Use the library in document and asset pipelines, or install the CLI as a `dotnet tool` to produce JSON reports for scripts and CI.

## Key capabilities

- **One metadata API across containers:** inspect individual fonts and collections through the same reader, retaining each face's original index.
- **Useful results from damaged inputs:** preserve independently readable metadata, with per-table statuses and structured diagnostics that distinguish missing data from unreadable data.
- **Catalog metadata:** collect variation axes and compact Unicode coverage ranges per face, with a separate reading status for each section.
- **Traceable license evidence:** follow an embedded hint to its original name record. For an external license document, retain its SHA-256 and the exact text and UTF-16 offsets behind each finding.
- **Offline inspection:** the core reader has no third-party runtime dependencies; optional SPDX expression analysis uses the pinned offline Spdx.Expressions package.
- **Automation through the same parser:** use the library or the CLI's versioned JSON reports, batch inputs, exit codes, resource limits and cooperative cancellation.

These capabilities support font catalogs, attribution reviews and metadata collection for audit or SBOM pipelines. Font.Metadata supplies font facts and license evidence; the consuming application owns policy decisions and SBOM generation.

## Font headers and table inventory in 1.1.0

Every face reports the raw OpenType revision, creation and modification times, design units, header flags and style when `head` is readable. The `maxp` table supplies the declared glyph count. Use the optional table inventory to compare physical variants with the same family and version.

```sh
font-metadata inspect example.woff2 --include-table-inventory --format json
```

Use `FontDiagnostic.LimitReason` to identify the reader setting behind `FM_LIMIT_EXCEEDED`. See the [reader API](docs/api.md#headers-and-table-inventory) for units and container-specific lengths. Distribution status is available on [NuGet](https://www.nuget.org/packages/Font.Metadata/) and [GitHub Releases](https://github.com/DmitryGaravsky/Font.Metadata/releases).

## Compatibility in 1.x

Starting with 1.0.0, the 1.x library contract preserves source and binary compatibility for supported .NET 10 callers of `Font.Metadata` and `Font.Metadata.Spdx`. The CLI emits JSON with `schemaVersion: 1`. Consumers should read fields by name and ignore additional optional fields. See the [API compatibility policy](docs/api.md#compatibility-and-evolution) and the [JSON v1 contract](docs/cli.md#json-contract-schema-version-1).

Independent validation of the earlier `0.1.0-dev` package passed all 135 sample/operation checks on a 45-sample reviewed corpus, with scoped FontTools/OTS comparisons. A separate Windows robustness campaign passed 62 contract/boundary checks and observed no execution failures in 256 seeded mutations; the initial research cycle is complete with documented limitations. Exploratory Parse/Analyze/I/O measurements did not confirm a product regression or establish stable release thresholds. See [Testing](docs/testing.md#independent-validation) for the evaluated package, coverage and remaining limits.

License analysis of name records recognizes a documented set of identifiers, names and URLs. The optional `Font.Metadata.Spdx` adapter adds source-preserving expression analysis through CLI `--analyze-spdx` and a separate analyzer for supplied license documents. It uses the published Spdx.Expressions 1.1.0 package. See the examples for [displaying license names and links](docs/spdx.md#display-metadata-for-a-recognized-license) and [resolving document-scoped references](docs/spdx.md#resolve-a-reference-in-a-supplied-document).

## Use as a dotnet tool

Install the `Font.Metadata.Tool` package to get the `font-metadata` command. Use the .NET 10 SDK to install it and the .NET 10 runtime to run it on Windows, Linux or macOS.

The following example uses a NuGet feed containing version 1.1.0.

```shell
dotnet tool install --global Font.Metadata.Tool --version 1.1.0
font-metadata inspect Variable.ttf --include-unicode-coverage --format json
font-metadata inspect Variable.ttf --include-name-records --analyze-licenses --format json
font-metadata inspect-license OFL.txt --format json
```

For example, with a Roboto Flex font the first command reports a `wght` axis with minimum 100, default 400 and maximum 1000, and a coverage range with `start: 32` and `end: 126` (U+0020 through U+007E). Check the section status alongside its values. Ordinary inspection includes declared axes. Add `--include-variation-axes` when an inventory also needs the status for static fonts and unreadable faces.

The next two commands show the two sources an auditor can trace. For Roboto Flex, one embedded `OFL-1.1` hint points to the text of name ID 13 on face 0. For the accompanying `OFL.txt`, the `fullText` entry identifies the normative OFL body. Use the reported SHA-256 to identify the file and `sourceStart` and `sourceLength` to locate the exact fragment in the decoded text. The caller records which document belongs with which font.

To try a local 1.1.0 build, run the following commands from the repository root with a fresh package directory and tool directory.

```shell
pwsh -File eng/verify.ps1 -Version 1.1.0 -PackageDirectory artifacts/local-packages
dotnet tool install --tool-path artifacts/local-tool --source artifacts/local-packages Font.Metadata.Tool --version 1.1.0
./artifacts/local-tool/font-metadata inspect Example.ttf --format json
```

Replace the sample font paths with your files. The tool package includes the reader, adapter and SPDX runtime dependency.

The tool accepts individual files, multiple files and binary stdin. Reports preserve partial metadata, section statuses and diagnostics. License analysis is opt-in; JSON has an explicit schema version and exit codes support automation. Directory enumeration and extraction of embedded fonts belong to the calling script or application.

See [CLI recipes](docs/cli-recipes.md) for global/local installation, repository manifests, directory inventories, license evidence, JSON processing and CI. The [CLI reference](docs/cli.md) defines arguments, exit codes and output behavior; the [JSON Schema](docs/cli.schema.json) describes machine-readable reports.

After dependency restore, run directly from source without installing a tool:

```shell
dotnet run --project Font.Metadata.Tool --no-restore -- inspect Example.woff2 --format json --analyze-licenses
```

## Inspect with the library

Package: `Font.Metadata`. Namespace: `Fonts.Metadata`.

Add the library to a .NET 10 project using a feed containing version 1.1.0.

```shell
dotnet add package Font.Metadata --version 1.1.0
```

For a local build, use the verified package directory as the NuGet source.

Use `Inspect` to retain readable values from damaged or partially supported inputs. `result.Faces` is safe to enumerate even when no metadata is available; inspect `Status` and `Diagnostics` to understand the outcome:

```csharp
using System;
using Fonts.Metadata;

FontInspectionResult result = FontMetadataReader.InspectFile("example.ttc");
Console.WriteLine(result.Status);
foreach(var face in result.Faces) {
    Console.WriteLine($"{face.Index}: {face.FamilyName}");
    Console.WriteLine($"name: {face.NameTableStatus}; OS/2: {face.Os2TableStatus}");
}
foreach(var diagnostic in result.Diagnostics) {
    Console.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
}
```

Use `Read` when the caller requires a complete result within the supported metadata scope. It uses the same parser and throws if that requirement is not met:

```csharp
using System;
using Fonts.Metadata;

FontMetadata metadata = FontMetadataReader.ReadFile("example.ttf");
foreach(FontMetadata.Face face in metadata.Faces) {
    Console.WriteLine($"{face.Index}: {face.FamilyName} / {face.SubfamilyName}");
}
```

These are separate examples. File, stream, byte array, span and memory inputs are available. See [API](docs/api.md) for overloads, exceptions, resource limits and ownership.

## Analyze license evidence

Analyze each available face independently, including faces retained in a partial inspection result:

```csharp
using System;
using Fonts.Metadata;

FontInspectionResult result = FontMetadataReader.InspectFile("example.ttf");
foreach(FontMetadata.Face face in result.Faces) {
    FontLicenseAnalysis analysis = FontLicenseAnalyzer.Analyze(face);
    Console.WriteLine($"Complete name source: {analysis.IsSourceComplete}");
    foreach(FontLicenseHint hint in analysis.Hints) {
        Console.WriteLine($"{hint.IdentifierKind}: {hint.Identifier} ({hint.RuleId}, record {hint.SourceRecord.Index})");
    }
}
```

The analyzer performs no I/O and preserves independent and conflicting indications. It recognizes a finite vocabulary of individual SPDX IDs, names, families and URLs; it does not parse compound expressions or exceptions. Empty hints do not prove that a font has no license. These are source indications, not permission or distribution decisions. See [License analysis](docs/licensing.md) for examples, recognition rules and boundaries.

Pass external license documents explicitly to the CLI for a separate audit report.

```shell
font-metadata inspect-license LICENSE OFL.txt NOTICE --format json
```

The report retains per-file status, SHA-256 of fully read bytes and evidence with exact text and UTF-16 offsets. The analyzer preserves every admitted SPDX declaration, including compound expressions, syntax errors and unknown identifiers. Its built-in catalog recognizes selected full texts and headings, including OFL 1.1, MIT and Apache-2.0. Library callers can supply a catalog per call and own any association between a document and a font. [Document API](docs/api.md#external-license-document-analysis) · [CLI contract and schema](docs/cli.md#external-license-documents)

## Supported metadata and boundaries

| Container | Available metadata |
| --- | --- |
| SFNT (TrueType/OpenType) | Source name records, preferred names and attribution, license strings, vendor ID and raw embedding flags |
| TTC/OTC | Per-face metadata with original indexes and shared-table support |
| WOFF | Metadata from raw or zlib-compressed tables |
| WOFF2 | Metadata from single fonts and collections using streamed Brotli decoding |
| EOT | Separate header facts and metadata from main/EUDC payloads |

Ordinary CLI `inspect` reports design axes for each face whose directory declares an `fvar` table. The section appears in both plain text and JSON (`variationAxes`). If the table is damaged, the section reports its status and any recovered axes. Use `--include-variation-axes` to audit every face, including those with a missing table or an unreadable directory.

Use `--include-unicode-coverage` to report inclusive Unicode scalar ranges mapped to nonzero glyph IDs. The reader selects one supported Unicode `cmap` subtable, preferring format 12 to format 4. In the .NET API, both reads are opt-in through `FontReadOptions.IncludeVariationAxes` and `FontReadOptions.IncludeUnicodeCoverage`.

Reading starts at the supplied input origin. The library does not search for embedded fonts, render glyphs or perform shaping. WOFF/WOFF2 XML metadata is not extracted. Supported table versions, encodings and container details are documented in [Formats](docs/formats.md).

`Complete` describes supported metadata extraction; it does not certify font validity, safety for rendering, or licensing. OS/2 `fsType` is exposed as raw embedding flags, separately from license evidence.

Resource limits and cancellation are part of the reader contract. They do not impose an exact process-memory ceiling or a hard execution deadline. See [Processing untrusted fonts](docs/security.md) for implemented protections and application responsibilities.

## Requirements

| Component | Target |
| --- | --- |
| Library | .NET 10 |
| CLI | .NET 10 |
| Build | .NET 10 SDK and PowerShell 7 |
| CI test matrix | .NET 10 on Windows and Linux for PRs; also macOS on main, manual runs and releases |

The build uses a stable .NET 10 SDK selected by global.json.

## Build and verify

```powershell
pwsh -File eng/verify.ps1
```

This restores dependencies, builds the solution, runs NUnit and offline release-script tests, checks C# whitespace, creates local development packages and verifies a PackageReference consumer, an installed dotnet tool and a portable payload. These checks are distinct from the completed initial independent validation cycle; a new release candidate still requires its own review.

Publishing a GitHub Release starts verification of the tagged commit, publication of the three NuGet packages and attachment of the verified packages, portable CLI ZIP and checksums. Publication requires the setup and readiness review in [Releases](docs/release.md). Local verification does not publish anything.

## Documentation

This README is the capability overview. The linked references own the detailed contracts: Formats for supported data, API and Diagnostics for reading outcomes, CLI and its schema for reports, and Testing for measured validation scope. Plans do not establish implemented behavior; source and tests must substantiate these descriptions.

- [API and usage](docs/api.md)
- [Formats and metadata](docs/formats.md)
- [Statuses and diagnostics](docs/diagnostics.md)
- [License analysis and evidence](docs/licensing.md)
- [CLI recipes and dotnet tool installation](docs/cli-recipes.md)
- [Command-line behavior](docs/cli.md)
- [Processing untrusted fonts](docs/security.md)
- [Architecture](docs/architecture.md)
- [Testing and validation scope](docs/testing.md)
- [Continuous integration](docs/ci.md)
- [Release preparation](docs/release.md)
- [Contributing](CONTRIBUTING.md)

## License

Font.Metadata is a personal project by [Dmitry Garavsky](https://github.com/DmitryGaravsky/).

Copyright (c) 2026 Dmitry Garavsky. Font.Metadata, Font.Metadata.Spdx and Font.Metadata.Tool are licensed under the [MIT License](LICENSE).
