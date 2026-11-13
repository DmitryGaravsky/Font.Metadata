# Architecture

The repository contains a .NET 10 metadata library, a CLI and two NUnit test projects. The library implements SFNT/TTC, WOFF, WOFF2 and EOT reading. The CLI exposes inspect, plain/JSON reports, binary stdin and optional license evidence. License evidence analysis is an independent operation on decoded records.

## Domain model

| Term | Current representation and boundary |
| --- | --- |
| Container | One supplied input origin. `FontMetadata` describes its format, available faces and independent EOT header facts; `FontInspectionResult` adds overall status and diagnostics. No public `FontContainer` type exists. |
| Font | A general domain term, not an additional public object or a globally identified font family. A file can contain several faces. |
| Face | `FontMetadata.Face`, with its original container index, source, decoded records, preferred values and section statuses. An unreadable bounded face can remain a placeholder. |
| Table | A tagged logical SFNT data section. Physical ranges and decoded buffers are internal; the public API exposes supported name/OS/2 metadata and statuses, not a general table inventory or `FontTable` object. |
| Record | `FontMetadata.NameRecord`: a successfully decoded name entry with original index, IDs and language context. It is not a copy of the encoded record bytes. |

```text
Input container → FontInspectionResult (status + diagnostics)
                       └── FontMetadata (container facts)
                            ├── Faces: original indexes + metadata
                            │    └── NameRecords → preferred values / license evidence
                            └── Eot: independent header facts, when available
```

TTC/OTC and WOFF2 collection faces can reference shared tables. Physical sharing does not merge faces or renumber their records; internal caches can reuse immutable decoded data, while diagnostics identify each affected face. Table ownership is therefore not a tree of exclusive byte buffers. Physical offsets and logical table offsets remain separate coordinates.

Reading names from a variable font does not enumerate its axes or named instances. The current model has no variation-instance identity, `Variations` property or general `Tables` collection. Adding those capabilities requires an explicit model and compatibility review; the present metadata model neither implements them nor proves that every future extension will be additive.

## Design principles

| Principle | Current interpretation |
| --- | --- |
| Metadata first | Extract supported metadata without rendering, shaping, installation or a complete OpenType validator. |
| Common model | Share face/record contracts across containers; keep EOT header facts separate and format-specific parsing internal. |
| Preserve evidence | Keep decoded values, duplicates, original indexes and provenance. Raw source-byte retention is not provided. |
| Partial results | Recover only through verified ranges; report container and section completeness independently. |
| Bounded processing | Account for work across the operation, including failed decoding and shared data. Budgets and cancellation do not establish a hard process-memory or time ceiling. |
| Deterministic metadata | For the same package, input and options, selection and evidence ordering do not depend on process culture. This is not a promise of identical OS I/O messages or results across different package versions. |
| No legal conclusions | Keep source evidence, recognition and application policy distinct. |
| Explicit contracts | Use immutable results and structured outcomes; review API and JSON compatibility before extending the experimental API. |
| Independent verification | Separate reviewed expectations, external-reader observations and product baselines; retain inconclusive findings. |
| Minimal dependencies | Keep reading independent of CLI, research infrastructure and consumer policy; add abstractions only for a concrete responsibility. |

These principles explain the implemented boundaries. They do not require a public plugin system, a generic table-source interface, DI infrastructure or additional packages. [Formats](formats.md), [API](api.md), [Security](security.md) and [Testing](testing.md) define the detailed scope.

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
│   ├── FontFaceSource.cs                container/main/EUDC provenance
│   ├── Reading/
│   │   ├── FontMetadataReader.cs        input ownership and Read/Inspect entry points
│   │   ├── FontReadOptions.cs           reading configuration
│   │   ├── FontReadLimits.cs            processing budgets
│   │   ├── FontReadException.cs         strict-read exceptions with inspection results
│   │   └── Internal/
│   │       ├── ContainerParser.cs       shared format dispatch and result assembly
│   │       ├── SfntParser.cs            SFNT/TTC directory validation and face recovery
│   │       ├── WoffParser.cs            independent raw/zlib metadata tables
│   │       ├── ZlibTableDecoder.cs      exact single-member zlib completion
│   │       ├── Woff2Parser*.cs          dictionary, collections and streamed Brotli
│   │       ├── EotParser.cs             separate header, main and EUDC recovery
│   │       ├── MtxDecoder.cs            bounded LZCOMP and optional RLE
│   │       ├── DecodeBudget.cs          shared work and live buffer accounting
│   │       ├── SourceLocation.cs        physical offsets and compressed anchors
│   │       ├── ContainerLayout.cs       checked shared container layout helpers
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
├── Font.Metadata.Tool/              arguments, input orchestration and plain/JSON report writers
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

The library has no application-layer dependencies, package dependencies or project references. It uses the .NET 10 runtime, including its code-page provider and zlib/Brotli codecs. MTX is decoded in managed code without native font APIs. The tool owns console behavior. Tests exercise public contracts. Deterministic cancellation checks use InternalsVisibleTo for name selection and operation-local cancellation checkpoints; no public test hooks or global switches are introduced.

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
            SFNT/TTC / WOFF / WOFF2 / EOT
                         ▼
       bounded header and directory validation
                         │
           raw or bounded decompression
              EOT header kept separately
                         │
             per-face independent recovery
                         ▼
           verified name and OS/2 ranges
                         │
       shared table + preferred names + string budget
                         ▼
       decoded records / preferred values / diagnostics
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

Options and limits are sealed, init-only values. An operation owns its diagnostics, source-aware table cache, string accounting, decoded-work budgets and live-buffer accounting; there are no global mutable caches or implicit encoding-provider registrations. Shared data is reused only within the operation. Each distinct name table owns its preferred-value selection; faces referencing that table reuse its selected strings. Returned records and diagnostics have no mutable backing arrays accessible to callers.

Distinct decoded buffers receive distinct source identities. Cache keys include source, tag, local offset and length; shared WOFF2/TTC tables reuse immutable metadata while diagnostics are localized per face. Raw/XOR data maps directly to input offsets; compressed errors use physical block anchors plus a separate logical table offset.

Brotli output is streamed through fixed scratch space and only selected metadata is retained. MTX validates all three streams and retains only a complete first CTF stream. Work counters survive expected failures; scoped buffers release retained capacity without refunding decoded work. Resizing accounts for both old and new capacities.

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
│   └── EotHeader (independent container facts)
├── FontReadStatus / FontSectionStatus / FontFormat / FontFaceSource
├── FontDiagnosticSeverity / FontDiagnosticCategory / FontDiagnosticCodes
└── FontReadException / FontFeatureNotSupportedException
```

Only data models nest under FontMetadata. Operations, options, status types and diagnostics remain independent. EotHeader is created only when additional verified facts exist beyond container recognition. FontLicenseAnalyzer operates on existing face records, without parser I/O or consumer-specific policy.

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

## CLI flow

```text
Validated arguments → one input → Inspect → optional Analyze per face
                                       → explicit JSON projection / plain writer
                                       → aggregate exit status
```

CommandOptions owns grammar; Command owns input lifetimes, cancellation and failure boundaries. StandardOutput uses a borrowed OS handle for redirected output so broken pipes remain observable as I/O failures; it never closes the process-owned handle. ReportValues defines the wire vocabulary, while JsonReportWriter and PlainReportWriter present results without accumulating the whole batch. Library models and parsers have no CLI dependency. The report contract is versioned separately in cli.schema.json.
