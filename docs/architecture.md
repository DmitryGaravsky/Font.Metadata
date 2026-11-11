# Architecture

The repository contains a .NET 10 metadata library, a CLI and two NUnit test projects. The library implements SFNT/TTC reading. The CLI currently exposes help/version; reporting and license analysis remain separate future components.

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

Source folders group responsibilities without defining additional public namespaces. Reading owns the operations, options, limits and strict-read exceptions; Inspection owns outcomes and completeness; Diagnostics owns diagnostic evidence and classification. Public types remain in `Fonts.Metadata`, and parsing helpers remain internal in `Fonts.Metadata.Internal` under `Reading/Internal`. The CLI and test projects keep their existing layouts.

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

Only data models nest under FontMetadata. Operations, options, status types and diagnostics remain independent. The reserved EotHeader model is not instantiated until EOT support exists. Future license analysis belongs in a separate FontLicenseAnalyzer operating on metadata and evidence, without parser I/O or consumer-specific policy.

## Runtime and validation boundaries

All four projects target net10.0. Repository settings, sources and dependencies are self-contained. No external consumer projects, source links or private feeds are required.

Complete describes supported metadata extraction, not comprehensive font validity. See [Formats](formats.md) for exactly what is checked and [Testing](testing.md) for verification.
