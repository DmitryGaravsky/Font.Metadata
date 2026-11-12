# Font.Metadata

[![Build and test (main)](https://github.com/DmitryGaravsky/Font.Metadata/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/DmitryGaravsky/Font.Metadata/actions/workflows/ci.yml?query=branch%3Amain)

Font.Metadata is an MIT-licensed .NET 10 library and command-line tool for font inventories and license evidence inspection.

Read names, attribution and declared license information from **TrueType/OpenType, TTC/OTC, WOFF, WOFF2 and EOT** files without installing fonts or loading a rendering engine. Use the library in document and asset pipelines, or install the CLI as a `dotnet tool` to produce JSON reports for scripts and CI.

## Key capabilities

- **One metadata API across containers:** inspect individual fonts and collections through the same reader, retaining each face's original index.
- **Useful results from damaged inputs:** preserve independently readable metadata, with per-table statuses and structured diagnostics that distinguish missing data from unreadable data.
- **Traceable license evidence:** retain decoded name records and link every recognized license hint to its source record, including conflicting indications.
- **Offline inspection with no third-party runtime dependencies:** the library uses .NET APIs; reading and license analysis make no network requests.
- **Automation through the same parser:** use the library or the CLI's versioned JSON reports, batch inputs, exit codes, resource limits and cooperative cancellation.

These capabilities support font catalogs, attribution reviews and metadata collection for audit or SBOM pipelines. Font.Metadata supplies font facts and license evidence; the consuming application owns policy decisions and SBOM generation.

## Project status

The core metadata readers and CLI inspection are implemented. Independent validation passed all 135 sample/operation checks on a 45-sample reviewed corpus, with scoped FontTools/OTS comparisons; broader validation continues ahead of the first preview release. See [Testing](docs/testing.md#independent-validation) for the evaluated package, coverage and remaining limits.

Current license analysis recognizes a documented set of identifiers, names and URLs. Standalone SPDX expression analysis and its integration into font inspection, followed by Unicode coverage, are planned extensions. They are not part of the current API; preview release readiness does not depend on their completion.

## Use as a dotnet tool

Install the `Font.Metadata.Tool` package to get the `font-metadata` command. Use the .NET 10 SDK to install it and the .NET 10 runtime to run it on Windows, Linux or macOS.

For a published package, replace `<version>` with an available release version:

```shell
dotnet tool install --global Font.Metadata.Tool --version <version>
font-metadata inspect Example.woff2 --format json --analyze-licenses
font-metadata inspect One.ttf Two.woff2 --include-name-records
```

Publication is separate from source builds; these instructions do not imply a public release is already available. To try the current source before publication, run from the repository root:

```shell
dotnet pack Font.Metadata.Tool/Font.Metadata.Tool.csproj -c Release -o artifacts/local-feed
dotnet tool install --tool-path artifacts/local-tool --add-source artifacts/local-feed Font.Metadata.Tool --version 0.1.0-dev
./artifacts/local-tool/font-metadata inspect Example.ttf --format json
```

Replace the sample font paths with your files. The local build example uses the current default development version and a fresh tool directory. The tool package includes the library; installing the library package separately is unnecessary.

The tool accepts individual files, multiple files and binary stdin. Reports preserve partial metadata, section statuses and diagnostics. License analysis is opt-in; JSON has an explicit schema version and exit codes support automation. Directory enumeration and extraction of embedded fonts belong to the calling script or application.

See [CLI recipes](docs/cli-recipes.md) for global/local installation, repository manifests, directory inventories, license evidence, JSON processing and CI. The [CLI reference](docs/cli.md) defines arguments, exit codes and output behavior; the [JSON Schema](docs/cli.schema.json) describes machine-readable reports.

To run directly from source without installing a tool:

```shell
dotnet run --project Font.Metadata.Tool -- inspect Example.woff2 --format json --analyze-licenses
```

## Inspect with the library

Package: `Font.Metadata`. Namespace: `Fonts.Metadata`.

Add a published version to a .NET 10 project, replacing `<version>` with an available release version:

```shell
dotnet add package Font.Metadata --version <version>
```

For a source build before publication, pack `Font.Metadata/Font.Metadata.csproj` and install from its output directory as a local NuGet feed. See [build and verify](#build-and-verify) for package verification.

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

## Supported metadata and boundaries

| Container | Available metadata |
| --- | --- |
| SFNT (TrueType/OpenType) | Source name records, preferred names and attribution, license strings, vendor ID and raw embedding flags |
| TTC/OTC | Per-face metadata with original indexes and shared-table support |
| WOFF | Metadata from raw or zlib-compressed tables |
| WOFF2 | Metadata from single fonts and collections using streamed Brotli decoding |
| EOT | Separate header facts and metadata from main/EUDC payloads |

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

This restores dependencies, builds the solution, runs NUnit and offline release-script tests, checks C# whitespace, creates local development packages and verifies a PackageReference consumer and an installed dotnet tool. These checks are distinct from the ongoing independent corpus validation.

Tagged releases publish the library and dotnet tool to NuGet.org and attach a portable CLI archive to GitHub Releases. Publication requires the setup and readiness review in [Releases](docs/release.md). Local verification does not publish anything.

## Documentation

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

Copyright (c) 2026 Dmitry Garavsky. Font.Metadata and Font.Metadata.Tool are licensed under the [MIT License](LICENSE).
