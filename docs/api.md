# API

Package: `Font.Metadata`. Namespace: `Fonts.Metadata`. Target: `net10.0`. All public types use this namespace, including types whose source files are grouped under Reading, Inspection, Diagnostics and Licensing; no additional namespace imports are needed.

## Public types

```text
FontMetadataReader                 static reading operations
├── Read / ReadFile                 complete metadata or an exception
└── Inspect / InspectFile           result with partial metadata and diagnostics

FontInspectionResult
├── Status                         FontReadStatus
├── Metadata?                      FontMetadata
│   ├── Format                     FontFormat
│   ├── Faces                      IReadOnlyList<FontMetadata.Face>
│   │   ├── Source                  FontFaceSource (Container / EotMain / EotEudc)
│   │   ├── NameTableStatus         FontSectionStatus
│   │   ├── Os2TableStatus          FontSectionStatus
│   │   └── NameRecords             IReadOnlyList<FontMetadata.NameRecord>
│   ├── EotHeaderStatus            FontSectionStatus
│   └── Eot?                       FontMetadata.EotHeader (independent facts)
└── Diagnostics                    IReadOnlyList<FontDiagnostic>

FontReadOptions
└── Limits                         FontReadLimits

FontLicenseAnalyzer.Analyze(Face)
└── FontLicenseAnalysis
    ├── IsSourceComplete           completeness of the name table
    └── Hints                      IReadOnlyList<FontLicenseHint>
        ├── Identifier / IdentifierKind / RuleId
        └── SourceRecord           original FontMetadata.NameRecord
```

The metadata and diagnostic types are sealed, immutable results with internal constructors. Nested models do not capture an outer instance. Collections do not expose mutable backing storage; results retain neither input buffers nor streams. An empty collection or a null property is interpreted together with its status, rather than as proof of absent source data.

## Ordinary reading

```csharp
using System;
using Fonts.Metadata;

FontMetadata font = FontMetadataReader.ReadFile("example.otf");
foreach(FontMetadata.Face face in font.Faces) {
    Console.WriteLine($"{face.Index}: {face.FullName ?? face.FamilyName}");
    Console.WriteLine($"Vendor: {face.VendorId}; fsType: {face.EmbeddingPermissions}");
}
```

`Read` and `Inspect` share one parser. `Read` returns only when the overall status is `Complete`, including a complete result with warnings. Otherwise:

- `Unsupported` throws `FontFeatureNotSupportedException : NotSupportedException`.
- `NotRecognized`, `Partial` and `Failed` throw `FontReadException : IOException`.
- Both exceptions expose the full `FontInspectionResult` through `Result`.

Missing optional metadata tables or a missing family string do not by themselves fail reading. “Complete” is limited to the [supported metadata](formats.md); this is not a full font validator.

## Inspection with partial results

```csharp
using System;
using Fonts.Metadata;

FontInspectionResult result = FontMetadataReader.InspectFile("example.ttc");
Console.WriteLine($"Read status: {result.Status}");

if(result.Metadata is { } font) {
    foreach(FontMetadata.Face face in font.Faces) {
        Console.WriteLine($"Face {face.Index}: name={face.NameTableStatus}, OS/2={face.Os2TableStatus}");
        foreach(FontMetadata.NameRecord record in face.NameRecords) {
            Console.WriteLine($"Record {record.Index}, name ID {record.NameId}: {record.Value}");
        }
        if(face.EmbeddingPermissions is ushort flags) {
            Console.WriteLine($"Raw embedding flags: 0x{flags:X4}");
        }
    }
}
foreach(FontDiagnostic diagnostic in result.Diagnostics) {
    Console.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
    Console.WriteLine($"Face={diagnostic.FaceIndex}, table={diagnostic.TableTag}, offset={diagnostic.ByteOffset}, tableOffset={diagnostic.TableByteOffset}");
}
```

A broken `name` table does not discard readable `OS/2` fields. A broken name record does not discard subsequent valid records. A broken TTC face does not discard other readable faces. Faces whose directories cannot be read remain as bounded placeholders with their original `Index`; a face limit omits indexes beyond the inspected prefix. Unknown container versions or truncated collection offset arrays can prevent face enumeration entirely.

Source record indexes also survive skipped records. Readable record values survive an unreadable format-1 language tag, with a null `LanguageTag` and a diagnostic. Such records remain available as source evidence but are excluded from preferred values; a valid alternative is used, or the preferred property remains null. See [status semantics](diagnostics.md).

## Inputs and resource ownership

`Read` and `Inspect` each accept:

| Input | Contract |
| --- | --- |
| `byte[]` | Null is rejected; input is not retained |
| `byte[], int offset, int count` | The slice is the container origin; invalid ranges throw |
| `ReadOnlySpan<byte>` | Parses without retaining the span |
| `ReadOnlyMemory<byte>` | Same parser and ownership as the span overload |
| `Stream` | Reads from current position; caller retains ownership |
| `ReadFile(string path)` / `InspectFile(string path)` | Opens and closes its own file stream |

All operations accept optional `FontReadOptions? options = null` and `CancellationToken cancellationToken = default`.

Offsets in diagnostics are relative to the supplied container origin, including for slices and streams positioned within a larger file. There is no implicit search or carving. TTC table offsets are relative to that same collection origin.

Seekable streams have their original position restored in `finally`, including after parse failure, I/O failure or cancellation. Position restoration itself can throw if the stream no longer permits seeking. Non-seekable streams are consumed. No caller-owned stream is disposed. Streams are buffered up to the configured input limit, with at most one extra byte consumed to detect oversized input.

Invalid arguments, I/O errors and cancellation propagate as exceptions from both entry points. `Inspect` converts expected font-data failures into diagnostics; it does not swallow arbitrary runtime exceptions. Synchronous reads cannot interrupt a currently blocked stream read; cancellation is checked around reads and during parsing.

Do not mutate input memory or concurrently reposition a stream during a call. Independent calls are safe to run concurrently; operation budgets and caches are independent.

## Limits

```csharp
using Fonts.Metadata;

var options = new FontReadOptions {
    Limits = new FontReadLimits {
        MaxInputBytes = 32 * 1024 * 1024,
        MaxFaces = 64,
        MaxNameRecords = 2048,
        MaxDiagnostics = 100
    }
};
FontInspectionResult result = FontMetadataReader.InspectFile("example.ttc", options);
```

Options and limits are sealed with init-only properties, so an operation can safely retain its validated options without a mutable snapshot.

| Limit | Default | Accounting |
| --- | ---: | --- |
| `MaxInputBytes` | 128 MiB | Whole supplied input; oversized input is rejected before parsing |
| `MaxTableBytes` | 16 MiB | Each supported metadata table |
| `MaxFaces` | 256 | Inspected collection/EOT faces, including unreadable main payloads |
| `MaxTablesPerFace` | 4096 | Directory entries; an oversized directory is not processed |
| `MaxNameRecords` | 4096 | Source records per distinct name table, including invalid ones |
| `MaxNameStringBytes` | 4 MiB | Distinct encoded string bytes attempted per name table (including referenced language tags), or per EOT header (four strings plus RootString) |
| `MaxTotalNameStringBytes` | 16 MiB | Same string work across the whole container |
| `MaxDiagnostics` | 256 | Reported diagnostics, including any final suppression marker |
| `MaxTotalDecodedBytes` | 128 MiB | All decompression output/work, including discarded bytes, secondary MTX streams and failed attempts |
| `MaxMtxPayloadBytes` | 64 MiB | LZ plus optional RLE output of all three streams of one MTX payload |
| `MaxExpansionRatio` | 1000 | Work per compressed unit divided by its compressed input length |
| `MaxWoff2Tables` | 16384 | Global WOFF2 dictionary entries |
| `MaxWoff2TableReferences` | 1048576 | All traversed collection references, including duplicates and skipped faces |
| `MaxRetainedDecodedBytes` | 64 MiB | Simultaneously live controlled buffer capacities, including LZ windows, XOR copies and resize overlap |

All limits must be positive. `MaxInputBytes` must also be at most `Array.MaxLength - 1`. Invalid options throw before reading. These limits bound processing work, not exact managed heap use; strings, records, diagnostics and buffering have additional overhead.

Repeated string references with the same range and encoding are decoded once per table. Exact shared table ranges with the same tag are parsed once across faces, including preferred-value selection. Repeated references to decoded strings reuse normalization results, including blank values. Invalid decode attempts count against string budgets; recovery never resets budgets. Strings that exceed a budget are skipped, allowing later smaller or already decoded strings to survive. A limit is reported explicitly and never silently produces `Complete`.

WOFF applies the ratio to each compressed table; WOFF2 to its full Brotli stream; MTX to each LZCOMP stream including that stream's optional RLE work. During recovery from a truncated or overlapping WOFF2 range, only available input before a bounded optional block contributes to the ratio; absent or conflicting bytes cannot enlarge the budget. Non-RLE output is counted once. Main and EUDC share the total work budget, while each MTX payload has its own payload cap. Failed work is not refunded; unused budget remains available to independent data. Raw/XOR copies consume retained capacity but are not decompression work. Retained capacity excludes caller input, returned strings, fixed scratch space and codec internals.

A WOFF2 face limit still requires bounded traversal of the remaining descriptors to locate compressed data. Every reference consumes the reference budget. Loss of framing or exhaustion of that budget prevents guessing the compressed start.

## EOT header and provenance

`EotHeaderStatus` is Missing with null `Eot` for non-EOT inputs, NotRead before EOT parsing (including input limits), Invalid for a malformed header without additional facts, and Partial when some facts survive. Complete refers to header structure independently of payload decoding.

`EotHeader` exposes nullable `FormatVersion` and `Flags` (uint), `EmbeddingPermissions` (ushort), `FamilyName`, `StyleName`, `FullName`, `Version` (strings), and `EudcCodePage`, `EudcFlags`, `EudcFontDataSize` (uint). FormatVersion is the container version; Version is header text. Nullable fields distinguish absence/unavailability from verified zero or empty values. Header strings preserve whitespace and NULs. RootString and Signature are not published.

`Face.Source` is `FontFaceSource.Container` for SFNT/TTC/WOFF/WOFF2, `EotMain` for index 0, and `EotEudc` for index 1. EUDC never becomes index 0 after main failure. Header facts never replace face fields or create name records. The overall Format stays Eot.

## Face and record properties

`FontMetadata.Face` exposes `Index`, `Source`, nullable raw `SfntFlavor`, both table states, source `NameRecords`, `VendorId`, and nullable raw `ushort EmbeddingPermissions`. Zero flags are distinct from missing flags.

Preferred string properties are `FamilyName`, `SubfamilyName`, `FullName`, `PostScriptName`, `Version`, `Copyright`, `Trademark`, `Manufacturer`, `Designer`, `Description`, `VendorUrl`, `DesignerUrl`, `LicenseDescription` and `LicenseUrl`.

Each `FontMetadata.NameRecord` exposes `Index`, `NameId`, `PlatformId`, `EncodingId`, `LanguageId`, nullable `LanguageTag`, and unmodified decoded `Value`. Selection rules and supported encodings are in [Formats](formats.md).

## License analysis

```csharp
using System;
using Fonts.Metadata;

FontInspectionResult inspection = FontMetadataReader.InspectFile("example.ttf");
if(inspection.Metadata is { } font) {
    foreach(FontMetadata.Face face in font.Faces) {
        FontLicenseAnalysis analysis = FontLicenseAnalyzer.Analyze(face);
        Console.WriteLine($"Source complete: {analysis.IsSourceComplete}");
        foreach(FontLicenseHint hint in analysis.Hints) {
            Console.WriteLine($"{hint.IdentifierKind} {hint.Identifier}: {hint.RuleId}, record {hint.SourceRecord.Index}");
        }
    }
}
```

`Analyze(FontMetadata.Face)` rejects null with `ArgumentNullException`, performs no I/O and does not alter metadata or reading diagnostics. It uses all available ID 13/14 records, including nonpreferred languages and partial data. `SourceRecord` is the original record object. Results and collections are immutable with internal constructors.

`IsSourceComplete` is true only for `NameTableStatus.Complete` or `Missing`, independently of other tables and the overall read status. It does not describe recognition coverage. Missing names, unsupported text or an empty result do not establish that a font has no license.

`FontLicenseIdentifierKind.Spdx` denotes a supported individual identifier; `Family` denotes a known family without a sufficiently specific version or variant. Hints preserve conflicting evidence. They neither select a final license nor synthesize an expression. Compound declarations and exceptions are deliberately not simplified. See [Licensing](licensing.md) for rule IDs, ordering, vocabulary, URL rules and rejected cases.

## Capability helpers and deferred features

`FontMetadataReader.IsFormatSupported(format)` reports implemented container readers: `Sfnt`, `Collection`, `Woff`, `Woff2` and `Eot`. `HasFontExtension(path)` recognizes .ttf, .otf, .ttc, .otc, .woff, .woff2 and .eot case-insensitively. An extension is not evidence of valid or supported content.

There is no embedded-font search API or CLI inspection yet. License strings and embedding flags are source facts; they do not establish permission to use or redistribute a font.
