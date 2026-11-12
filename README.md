# Font.Metadata

Font.Metadata is a .NET 10 library for reading font metadata, with a companion command-line project. It is a personal project by [Dmitry Garavsky](https://github.com/DmitryGaravsky/).

The current development version reads **SFNT (TrueType/OpenType), TTC/OTC, WOFF, WOFF2 and EOT**. It exposes names, vendor identifiers and raw embedding flags, preserves partial results, and reports table status and structured diagnostics.

## Read a font

Package: `Font.Metadata`. Namespace: `Fonts.Metadata`.

```csharp
using System;
using Fonts.Metadata;

FontMetadata metadata = FontMetadataReader.ReadFile("example.ttf");
foreach(FontMetadata.Face face in metadata.Faces) {
    Console.WriteLine($"{face.Index}: {face.FamilyName} / {face.SubfamilyName}");
    Console.WriteLine($"License text: {face.LicenseDescription}");
}
```

`Read` requires complete supported metadata. Use `Inspect` to retain readable values from damaged or partially supported inputs:

```csharp
using System;
using Fonts.Metadata;

FontInspectionResult result = FontMetadataReader.InspectFile("example.ttc");
Console.WriteLine(result.Status);
if(result.Metadata is { } metadata) {
    foreach(var face in metadata.Faces) {
        Console.WriteLine($"{face.Index}: {face.FamilyName}");
        Console.WriteLine($"name: {face.NameTableStatus}; OS/2: {face.Os2TableStatus}");
    }
}
foreach(var diagnostic in result.Diagnostics) {
    Console.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
}
```

These are separate examples. See [API](docs/api.md) for input overloads, exceptions, limits and ownership.

## Available functionality

- SFNT flavors: TrueType 1.0, `OTTO`, `true` and `typ1`; TTC versions 1 and 2.
- WOFF raw/zlib tables and WOFF2 single fonts/collections with streamed Brotli decoding.
- EOT versions 1, 2.1 and 2.2: separate header facts, raw/XOR/MTX payloads, LZCOMP/RLE and independent EUDC.
- `name` table formats 0/1: source records, language tags, Unicode, MacRoman and supported legacy Windows encodings.
- Preferred identity, attribution, description, URL and license strings.
- `OS/2` metadata: vendor identifier and raw `fsType` embedding flags.
- Independent table states, stable diagnostic codes, bounded processing and cancellation.
- File, stream, byte array, span and memory inputs; immutable results.
- Separate license analysis of every available name ID 13/14 record, with stable rules and source evidence.

Reading starts at the supplied input origin. The library does not search for embedded fonts. “Complete” describes supported metadata extraction, not validation of outlines, SFNT table checksums or licensing. See [formats and boundaries](docs/formats.md).

The `Font.Metadata.Tool` package inspects files or binary stdin with plain/JSON reports, partial metadata and optional license evidence. See [CLI commands and JSON Schema](docs/cli.md).

```shell
font-metadata inspect Example.ttf --format json --analyze-licenses
font-metadata inspect One.ttf Two.woff2 --include-name-records
```

## Analyze license evidence

```csharp
using System;
using Fonts.Metadata;

FontMetadata metadata = FontMetadataReader.ReadFile("example.ttf");
foreach(FontMetadata.Face face in metadata.Faces) {
    FontLicenseAnalysis analysis = FontLicenseAnalyzer.Analyze(face);
    Console.WriteLine($"Complete name source: {analysis.IsSourceComplete}");
    foreach(FontLicenseHint hint in analysis.Hints) {
        Console.WriteLine($"{hint.IdentifierKind}: {hint.Identifier} ({hint.RuleId}, record {hint.SourceRecord.Index})");
    }
}
```

The analyzer performs no I/O and preserves independent and conflicting indications. It recognizes a finite vocabulary of individual SPDX IDs, names, families and URLs; it does not simplify compound expressions or exceptions. Empty hints do not prove that a font has no license. These are source indications, not permission or distribution decisions. See [license analysis](docs/licensing.md) for the exact rules and limitations.

## Requirements

| Component | Target |
| --- | --- |
| Library | .NET 10 |
| CLI | .NET 10 |
| Build | .NET 10 SDK and PowerShell 7 |
| CI test matrix | .NET 10 on Windows, Linux and macOS |

The library and tool require .NET 10. The build uses a stable .NET 10 SDK selected by global.json.

## Build and verify

```powershell
pwsh -File eng/verify.ps1
```

This restores dependencies, builds the solution, runs NUnit and offline release-script tests, checks C# whitespace, creates local development packages and verifies a PackageReference consumer and an installed dotnet tool.

Run the CLI from source:

```shell
dotnet run --project Font.Metadata.Tool -- --help
dotnet run --project Font.Metadata.Tool -- --version
```

Tagged releases publish the library and dotnet tool to NuGet.org and attach a portable CLI archive to GitHub Releases. Publication requires the one-time setup in [Releases](docs/release.md). Local verification does not publish anything or establish that a public version is available.

## License

Copyright (c) 2026 Dmitry Garavsky. Font.Metadata and Font.Metadata.Tool are licensed under the [MIT License](LICENSE).

## Documentation

- [API and usage](docs/api.md)
- [Formats and metadata](docs/formats.md)
- [Statuses and diagnostics](docs/diagnostics.md)
- [License analysis and evidence](docs/licensing.md)
- [Command-line behavior](docs/cli.md)
- [Architecture](docs/architecture.md)
- [Testing](docs/testing.md)
- [Continuous integration](docs/ci.md)
- [Release preparation](docs/release.md)
- [Contributing](CONTRIBUTING.md)
