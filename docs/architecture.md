# Architecture

The repository contains a .NET 10 metadata library, a CLI and two NUnit test projects. The library implements SFNT/TTC reading. The CLI currently exposes help/version; reporting remains a future component. License evidence analysis is an independent operation on decoded records.

## Repository layout

```text
Font.Metadata/
├── Font.Metadata.slnx
├── Directory.Build.props           compiler/package settings and development version
├── Directory.Packages.props        central dependency versions
├── global.json                     .NET SDK selection
├── NuGet.Config                    public dependency source
├── Font.Metadata/
│   ├── FontMetadata.cs                  immutable metadata and nested models
│   ├── FontFormat.cs                    source container format
│   ├── Reading/
│   │   ├── FontMetadataReader.cs        input ownership and Read/Inspect entry points
│   │   ├── FontReadOptions.cs           reading configuration
│   │   ├── FontReadLimits.cs            processing budgets
│   │   ├── FontReadException.cs         strict-read exceptions with inspection results
│   │   └── Internal/
│   │       ├── SfntParser.cs            container/directory validation and face recovery
│   │       ├── MetadataTables.cs        name and OS/2 decoding
│   │       ├── NameTableDefinitions.cs  shared name/platform/encoding identifiers
│   │       ├── NameSelection.cs         deterministic preferred values
│   │       └── ReadingContext.cs        operation budgets, caches and diagnostics
│   ├── Licensing/
│   │   ├── FontLicenseAnalyzer.cs      independent analysis of decoded name records
│   │   ├── FontLicenseAnalysis.cs      immutable hints and source completeness
│   │   ├── FontLicenseHint.cs          identifier, rule and original source record
│   │   ├── FontLicenseIdentifierKind.cs individual SPDX identifier or family
│   │   └── Internal/
│   │       ├── LicenseRules.cs         frozen finite identifier/name/family vocabulary
│   │       ├── LicenseText.cs          bounded-context statement and header matching
│   │       └── LicenseUrls.cs          structural URI validation and exact allowlist
│   ├── Inspection/
│   │   ├── FontInspectionResult.cs     metadata, completion status and diagnostics
│   │   ├── FontReadStatus.cs           overall reading outcome
│   │   └── FontSectionStatus.cs        per-table completeness
│   └── Diagnostics/
│       ├── FontDiagnostic.cs           diagnostic evidence and source location
│       ├── FontDiagnosticCodes.cs      stable diagnostic identifiers
│       ├── FontDiagnosticCategory.cs   diagnostic classification
│       └── FontDiagnosticSeverity.cs   diagnostic severity
├── Font.Metadata.Tool/              help, version and unsupported-command handling
├── Font.Metadata.Tests/             public API, synthetic fonts and recovery tests
├── Font.Metadata.Tool.Tests/        child-process CLI tests
├── eng/                            local verification, packaging and release scripts
├── .github/workflows/ci.yml         operating-system verification matrix
├── .github/workflows/release.yml    explicit version-tag publication
└── docs/                           product and contributor documentation
```

Source folders group responsibilities without defining additional public namespaces. Reading owns the operations, options, limits and strict-read exceptions; Inspection owns outcomes and completeness; Diagnostics owns diagnostic evidence and classification; Licensing owns independent license indications. Matching helpers use Fonts.Metadata.Internal under Licensing/Internal. Public types remain in `Fonts.Metadata`, and parsing helpers remain internal in `Fonts.Metadata.Internal` under `Reading/Internal`. The CLI and test projects keep their existing layouts.

## Dependencies

```text
 Font.Metadata.Tests            Font.Metadata.Tool.Tests
          │                              │
          │ ProjectReference             │ build reference and child process
          ▼                              ▼
 Font.Metadata ◀────────────── Font.Metadata.Tool
                         ProjectReference

 Local library nupkg ──→ isolated PackageReference consumer
 Local tool nupkg ─────→ isolated tool-path installation
```

The library has no application-layer dependencies, package dependencies or project references. It uses the .NET 10 runtime, including its code-page provider. The tool owns console behavior. Tests exercise public contracts. A deterministic cancellation check uses InternalsVisibleTo for the existing internal name-selection operation; no public test hooks are introduced.

## Reading flow

```text
 File / Stream                 Array / Slice / Memory / Span
       │                                    │
       ▼                                    │
 owned file lifetime; bounded stream buffer │
 restore caller's seekable stream position  │
       └─────────────────┬──────────────────┘
                         ▼
           one operation / leading input origin
                         │
                   SFNT or TTC
                         ▼
       bounded header and directory validation
                         │
             per-face independent recovery
                         ▼
           verified name and OS/2 ranges
                         │
       shared table + preferred names + string budget
                         ▼
       raw records / preferred values / diagnostics
                         │
                         ▼
             immutable FontInspectionResult
                    ┌────┴────┐
                    ▼         ▼
                 Inspect     Read
                 returns     requires Complete
                             otherwise throws with Result
```

All offsets and sizes are validated before slicing or allocation. Independent tables and faces survive local data failures. Collection and source-record indexes are preserved. Format constraints and configurable budgets are distinct. Read and Inspect never diverge into separate parsers.

Options and limits are sealed, init-only values. An operation owns its diagnostics, table cache and string accounting; there are no global mutable caches or implicit encoding-provider registrations. Shared data is reused only within the operation. Each distinct name table owns its preferred-value selection; faces referencing that table reuse its selected strings. Returned records and diagnostics have no mutable backing arrays accessible to callers.

## Public ownership

```text
namespace Fonts.Metadata
├── FontMetadataReader
├── FontLicenseAnalyzer ──→ FontLicenseAnalysis ──→ FontLicenseHint
│                                                ├── FontLicenseIdentifierKind
│                                                └── FontMetadata.NameRecord (original object)
├── FontReadOptions ──→ FontReadLimits
├── FontInspectionResult ──→ FontDiagnostic
├── FontMetadata
│   ├── Face ──→ NameRecord
│   ├── NameRecord
│   └── EotHeader (reserved)
├── FontReadStatus / FontSectionStatus / FontFormat
├── FontDiagnosticSeverity / FontDiagnosticCategory / FontDiagnosticCodes
└── FontReadException / FontFeatureNotSupportedException
```

Only data models nest under FontMetadata. Operations, options, status types and diagnostics remain independent. The reserved EotHeader model is not instantiated until EOT support exists. FontLicenseAnalyzer operates on existing face records, without parser I/O or consumer-specific policy.

## License analysis flow

```text
FontMetadata.Face
├── NameTableStatus ───────────────────────────→ IsSourceComplete
└── NameRecords (all languages, source order)
    └── IDs 13/14
        └── statement / annotation-line / versioned-header boundaries
            └── complete text lookup or structural URI allowlist
                └── per-record evidence deduplication
                    └── record order → rule order → evidence order
                        └── immutable FontLicenseAnalysis
                            └── hints retain original NameRecord objects
```

Matching uses fixed immutable dictionaries and iterative scanning; it does not use recursive expression parsing, regex backtracking, network lookups or global mutable caches. Each analysis invocation reuses matching results for shared decoded string objects, while constructing separate hints for every source record. Temporary matching state is not retained in results.

Known expressions, exceptions and unknown variants fail whole-declaration matching; no operand is extracted as an unqualified license. Quote, parenthesis and URL boundaries prevent text inside them from being treated as separate statements. Independent records and explicit statements remain analyzable. See [Licensing](licensing.md) for the finite supported grammar and its limits.

## Runtime and validation boundaries

All four projects target net10.0. Repository settings, sources and dependencies are self-contained. No external consumer projects, source links or private feeds are required.

Complete describes supported metadata extraction, not comprehensive font validity. See [Formats](formats.md) for exactly what is checked and [Testing](testing.md) for verification.
