# API

Package: `Font.Metadata`. Namespace: `Fonts.Metadata`. Target: `net10.0`. All public types in the core package use this namespace, including types whose source files are grouped under Reading, Inspection, Diagnostics and Licensing; no additional namespace imports are needed.

## Public types

```text
FontMetadataReader                 static reading operations
├── Read / ReadFile                 complete metadata or an exception
└── Inspect / InspectFile           result with partial metadata and diagnostics

FontInspectionResult
├── Status                         FontReadStatus
├── Faces                          IReadOnlyList<FontMetadata.Face> (never null)
├── Metadata?                      FontMetadata
│   ├── Format                     FontFormat
│   ├── Faces                      IReadOnlyList<FontMetadata.Face>
│   │   ├── Source                  FontFaceSource (Container / EotMain / EotEudc)
│   │   ├── NameTableStatus         FontSectionStatus
│   │   ├── Os2TableStatus          FontSectionStatus
│   │   ├── HeadTableStatus         FontSectionStatus
│   │   ├── Head?                   FontHeader
│   │   ├── MaxpTableStatus         FontSectionStatus
│   │   ├── GlyphCount?             ushort
│   │   ├── TableInventory?         FontTableInventory (Status and Entries)
│   │   ├── UnicodeCoverage?        FontUnicodeCoverage
│   │   ├── VariationAxes?          FontVariationAxes
│   │   └── NameRecords             IReadOnlyList<FontMetadata.NameRecord>
│   ├── EotHeaderStatus            FontSectionStatus
│   └── Eot?                       FontMetadata.EotHeader (independent facts)
└── Diagnostics                    IReadOnlyList<FontDiagnostic>

FontReadOptions
├── IncludeUnicodeCoverage / IncludeVariationAxes / IncludeTableInventory
└── Limits                         FontReadLimits

FontLicenseAnalyzer.Analyze(Face)
└── FontLicenseAnalysis
    ├── IsSourceComplete           completeness of the name table
    └── Hints                      IReadOnlyList<FontLicenseHint>
        ├── Identifier / IdentifierKind / RuleId
        └── SourceRecord           original FontMetadata.NameRecord
```

Metadata and diagnostics are immutable. Scalar ranges are readonly value types. Nested models do not capture an outer instance. Collections do not expose mutable backing storage; results retain neither input buffers nor streams. An empty collection or a null property is interpreted together with its status, rather than as proof of absent source data.

## Compatibility and evolution

The 1.x compatibility policy takes effect with 1.0.0. Package validation for 1.1.0 compares both public libraries against their 1.0.0 packages.

| Surface | Commitment for 1.x |
| --- | --- |
| .NET callers | Existing supported calls continue to compile and existing compiled net10.0 consumers continue to run. Preserve public type and assembly identities, member signatures, parameter names, optional argument defaults and nullability contracts. Review new overloads for ambiguity. Removing a supported target or making an incompatible API change requires a new major version. |
| SPDX adapter | The public registry, parser and validation types from Spdx.Expressions are part of the adapter contract. Review the exact dependency version and its types when upgrading. The core reader stays independently usable. |
| Results and failures | Preserve immutable results, stream ownership and position rules, cancellation and I/O exceptions, and strict `Read` versus recoverable `Inspect` behavior. A verified zero, an empty value, an unavailable value and a proven missing section keep their distinct meanings. Preserve face and record source indexes, including gaps, and the documented coordinate system for each offset. |
| Enums and diagnostics | Existing enum values keep their numeric values and meanings. Handle unfamiliar values explicitly. Diagnostic codes and license rule IDs keep their meanings. Use codes for decisions and messages for display. |
| Processing and recognition | Review changes to default limits, preferred-name selection and license recognition against existing inputs. Document observable fixes and rule or catalog updates. Retain exact source evidence, distinguish recognition and syntax profiles from registry identity, and preserve the meaning of source and analysis completeness. |

An optional analysis owns its completeness, limits and diagnostics. A face-level Unicode coverage or variation-axis analysis may be incomplete while the default inspection still returns its verified names. Container-level evidence would retain its container source even when the container holds several faces. With such analyses disabled, existing calls retain their status and exception semantics, including errors already detected by container framing checks.

The CLI publishes its own [versioned JSON contract](cli.md#json-contract-schema-version-1). The [package compatibility check](testing.md#package-compatibility) covers the two library packages.

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
foreach(FontMetadata.Face face in result.Faces) {
    Console.WriteLine($"Face {face.Index}: name={face.NameTableStatus}, OS/2={face.Os2TableStatus}");
    foreach(FontMetadata.NameRecord record in face.NameRecords) {
        Console.WriteLine($"Record {record.Index}, name ID {record.NameId}: {record.Value}");
    }
    if(face.EmbeddingPermissions is ushort flags) {
        Console.WriteLine($"Raw embedding flags: 0x{flags:X4}");
    }
}
foreach(FontDiagnostic diagnostic in result.Diagnostics) {
    Console.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
    Console.WriteLine($"Face={diagnostic.FaceIndex}, table={diagnostic.TableTag}, offset={diagnostic.ByteOffset}, tableOffset={diagnostic.TableByteOffset}");
}
```

`FontInspectionResult.Faces` is a non-null, read-only view of `Metadata.Faces`, or an empty read-only collection when `Metadata` is null. It preserves the original face objects, source order, indexes and unreadable-face placeholders; it does not filter to complete faces. No metadata is fabricated and `Metadata` remains nullable. Container facts such as `Format` and the EOT header still require checking `Metadata`.

An empty `Faces` collection does not establish success or the absence of container metadata. A recognized container can retain metadata without enumerable faces. Use `Status` for overall completeness and each face's section statuses for its available data. `Metadata is { } metadata` checks whether container metadata is present; it does not establish successful reading. Diagnostics explain the outcome, but checking for error-severity entries is not a substitute for `Status`: `NotRecognized` can have no diagnostics, and a diagnostic limit can replace errors with a truncation warning.

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
| `MaxDiagnostics` | 256 | Container-wide slots for baseline and optional section diagnostics combined, including suppression markers |
| `MaxTotalDecodedBytes` | 128 MiB | All decompression output/work, including discarded bytes, secondary MTX streams and failed attempts |
| `MaxTotalCmapWork` | 1,048,576 | cmap encoding records, groups, segments and individual glyph-array entries examined per container |
| `MaxTotalUnicodeRanges` | 65,536 | Compact ranges retained from distinct cmap tables per container |
| `MaxTotalVariationAxes` | 16,384 | fvar axis records examined across distinct tables, including invalid records |
| `MaxMtxPayloadBytes` | 64 MiB | LZ plus optional RLE output of all three streams of one MTX payload |
| `MaxExpansionRatio` | 1000 | Work per compressed unit divided by its compressed input length |
| `MaxWoff2Tables` | 16384 | Global WOFF2 dictionary entries |
| `MaxWoff2TableReferences` | 1048576 | All traversed collection references, including duplicates and skipped faces |
| `MaxRetainedDecodedBytes` | 64 MiB | Simultaneously live controlled buffer capacities, including LZ windows, XOR copies and resize overlap |

All limits must be positive. `MaxInputBytes` must also be at most `Array.MaxLength - 1`. Invalid options throw before reading. These limits bound processing work, not exact managed heap use; strings, records, diagnostics and buffering have additional overhead.

Baseline diagnostics take priority across all faces. Distinct optional sections share the remaining diagnostic slots in processing order. A shared table reuses its section and diagnostics on each referencing face. If optional messages overflow the remaining slots, the last available optional slot contains `FM_DIAGNOSTICS_TRUNCATED`. If baseline diagnostics use every slot, the optional lists are empty. Section statuses still describe the underlying data.

Repeated string references with the same range and encoding are decoded once per table. Exact shared table ranges with the same tag are parsed once across faces, including preferred-value selection. Repeated references to decoded strings reuse normalization results, including blank values. Invalid decode attempts count against string budgets; recovery never resets budgets. Strings that exceed a budget are skipped, allowing later smaller or already decoded strings to survive. A limit is reported explicitly and never silently produces `Complete`.

WOFF applies the ratio to each compressed table; WOFF2 to its full Brotli stream; MTX to each LZCOMP stream including that stream's optional RLE work. During recovery from a truncated or overlapping WOFF2 range, only available input before a bounded optional block contributes to the ratio; absent or conflicting bytes cannot enlarge the budget. Non-RLE output is counted once. Main and EUDC share the total work budget, while each MTX payload has its own payload cap. Failed work is not refunded; unused budget remains available to independent data. Raw/XOR copies consume retained capacity but are not decompression work. Retained capacity excludes caller input, returned strings, fixed scratch space and codec internals.

A WOFF2 face limit still requires bounded traversal of the remaining descriptors to locate compressed data. Every reference consumes the reference budget. Loss of framing or exhaustion of that budget prevents guessing the compressed start.

## EOT header and provenance

`EotHeaderStatus` is Missing with null `Eot` for non-EOT inputs, NotRead before EOT parsing (including input limits), Invalid for a malformed header without additional facts, and Partial when some facts survive. Complete refers to header structure independently of payload decoding.

`EotHeader` exposes nullable `FormatVersion` and `Flags` (uint), `EmbeddingPermissions` (ushort), `FamilyName`, `StyleName`, `FullName`, `Version` (strings), and `EudcCodePage`, `EudcFlags`, `EudcFontDataSize` (uint). FormatVersion is the container version; Version is header text. Nullable fields distinguish absence/unavailability from verified zero or empty values. Header strings preserve whitespace and NULs. RootString and Signature are not published.

`Face.Source` is `FontFaceSource.Container` for SFNT/TTC/WOFF/WOFF2, `EotMain` for index 0, and `EotEudc` for index 1. EUDC never becomes index 0 after main failure. Header facts never replace face fields or create name records. The overall Format stays Eot.

## Face and record properties

`FontMetadata.Face` exposes `Index`, `Source`, nullable raw `SfntFlavor`, `NameTableStatus`, `Os2TableStatus`, `HeadTableStatus`, `MaxpTableStatus`, source `NameRecords`, `VendorId`, and nullable raw `ushort EmbeddingPermissions`. Zero flags are distinct from missing flags. Use `Head` for raw header fields, `GlyphCount` for the declared count and `TableInventory` for the optional directory. See [Headers and table inventory](#headers-and-table-inventory).

Preferred string properties are `FamilyName`, `SubfamilyName`, `FullName`, `PostScriptName`, `Version`, `Copyright`, `Trademark`, `Manufacturer`, `Designer`, `Description`, `VendorUrl`, `DesignerUrl`, `LicenseDescription` and `LicenseUrl`.

Each `FontMetadata.NameRecord` exposes `Index`, `NameId`, `PlatformId`, `EncodingId`, `LanguageId`, nullable `LanguageTag`, and unmodified decoded `Value`. Selection rules and supported encodings are in [Formats](formats.md).

## Optional catalog metadata

```csharp
using System;
using Fonts.Metadata;

var options = new FontReadOptions { IncludeUnicodeCoverage = true, IncludeVariationAxes = true };
var result = FontMetadataReader.InspectFile("example.ttf", options);
foreach(var face in result.Faces) {
    if(face.UnicodeCoverage is { Status: FontSectionStatus.Complete } coverage) {
        foreach(var range in coverage.Ranges)
            Console.WriteLine($"U+{range.Start:X4}..U+{range.End:X4}");
    }
    if(face.VariationAxes is { } variations) {
        foreach(var axis in variations.Axes)
            Console.WriteLine($"{axis.Tag} [{axis.Minimum}, {axis.Maximum}] (name ID {axis.NameId})");
    }
}
```

Both library switches default to false, leaving the corresponding face property null. With fvar reading enabled, `HasTableEntry` records whether the reader found an fvar directory entry, independently of how many axes it recovered. The CLI uses this evidence to show declared fvar tables by default. Its explicit audit flag also shows faces where the table is missing or its presence is unknown. A requested section always has a status. A partial result can retain verified ranges or axes. Its diagnostics belong to that face’s section and may be shared when several faces reference the same physical table. See [format boundaries](formats.md#optional-face-tables).

## License analysis

```csharp
using System;
using Fonts.Metadata;

FontInspectionResult inspection = FontMetadataReader.InspectFile("example.ttf");
foreach(FontMetadata.Face face in inspection.Faces) {
    FontLicenseAnalysis analysis = FontLicenseAnalyzer.Analyze(face);
    Console.WriteLine($"Source complete: {analysis.IsSourceComplete}");
    foreach(FontLicenseHint hint in analysis.Hints) {
        Console.WriteLine($"{hint.IdentifierKind} {hint.Identifier}: {hint.RuleId}, record {hint.SourceRecord.Index}");
    }
}
```

`Analyze(FontMetadata.Face)` rejects null with `ArgumentNullException`, performs no I/O and does not alter metadata or reading diagnostics. It uses all available ID 13/14 records, including nonpreferred languages and partial data. `SourceRecord` is the original record object. Results and collections are immutable with internal constructors.

`IsSourceComplete` is true only for `NameTableStatus.Complete` or `Missing`, independently of other tables and the overall read status. It does not describe recognition coverage. Missing names, unsupported text or an empty result do not establish that a font has no license.

`FontLicenseIdentifierKind.Spdx` denotes a supported individual identifier; `Family` denotes a known family without a sufficiently specific version or variant. Hints preserve conflicting evidence. They neither select a final license nor synthesize an expression. Compound declarations and exceptions are deliberately not simplified. See [Licensing](licensing.md) for rule IDs, ordering, vocabulary, URL rules and rejected cases.

## Capability helpers and deferred features

`FontMetadataReader.IsFormatSupported(format)` reports implemented container readers: `Sfnt`, `Collection`, `Woff`, `Woff2` and `Eot`. `HasFontExtension(path)` recognizes .ttf, .otf, .ttc, .otc, .woff, .woff2 and .eot case-insensitively. An extension is not evidence of valid or supported content.

There is no embedded-font search API. The [CLI](cli.md) exposes inspection and optional license analysis. License strings and embedding flags are source facts; they do not establish permission to use or redistribute a font.

## SPDX expression analysis

The optional `Font.Metadata.Spdx` package adds `FontSpdxAnalyzer`, `FontSpdxOptions`, `FontSpdxAnalysis`, `FontSpdxDeclaration` and `FontSpdxDiagnostic` in `Fonts.Metadata`. It reuses native AST and validation types from the `Spdx.Expressions` namespace; import that namespace when supplying the registry or parser/validation options. See [the complete API, recognition and limit contract](spdx.md). The reader and legacy hint API remain independent.

## External license document analysis

`Font.Metadata.Spdx` also exposes an offline document analyzer in `Fonts.Metadata`.

```csharp
using System;
using Fonts.Metadata;
using Spdx.Expressions;

string text = "Components\r\nSPDX-License-Identifier: MIT OR Apache-2.0\r\n";
FontLicenseDocumentAnalysis result =
    FontLicenseDocumentAnalyzer.Analyze(text, SpdxLicenseRegistry.Default);
foreach(FontLicenseDocumentEvidence item in result.Evidence)
    Console.WriteLine($"{item.Origin} [{item.SourceStart}, {item.SourceLength}]: {item.SourceText}");
```

`Analyze(string text, SpdxLicenseRegistry registry, FontLicenseDocumentOptions? options = null, CancellationToken cancellationToken = default)` accepts already decoded text. The caller owns file discovery, decoding and document-to-font association. Analysis performs no I/O and makes no licensing decision. A null `text` or `registry` throws `ArgumentNullException`. A limit below 1 throws `ArgumentOutOfRangeException`. Cancellation throws `OperationCanceledException` without returning a partial result.

`FontLicenseDocumentAnalysis` contains `IsAnalysisComplete`, `RegistryIdentity`, a read-only `Evidence` list in source order and a nullable document-level `Diagnostic` of type `FontLicenseDocumentDiagnostic`. Every `FontLicenseDocumentEvidence` contains `Origin`, optional `Identifier`, and `SourceStart`, `SourceLength`, `SourceText`. Declarations additionally expose `ExpressionStart`, `ExpressionLength`, `ExpressionText`, `ParseResult` and nullable `ValidationResult`. Successful parses keep the native immutable SPDX AST and validation results. References remain unresolved and multiple declarations remain independent.

All document offsets are zero-based UTF-16 code-unit indexes into the supplied string. `SourceText` equals `text.Substring(SourceStart, SourceLength)`. A single initial U+FEFF is ignored for recognition and counted in offsets. A declaration's source fragment is the entire line excluding its BOM and line ending. Its parser input excludes surrounding ASCII spaces and tabs after the colon. An empty suffix has zero length and starts immediately after the colon. Native parse and validation spans are relative to `ExpressionText`; adding `ExpressionStart` maps a native span start to the document. Headers and bodies exclude their outer ASCII whitespace and retain their interior whitespace.

### Recognition profile

The `license-document-v1` recognition rules produce three kinds of evidence.

| Origin | Recognized evidence |
| --- | --- |
| `SpdxDeclaration` | The case-sensitive prefix `SPDX-License-Identifier:` at the start of a line after optional spaces/tabs, with the remainder as one expression. CRLF, LF and CR are line endings. Every admitted attempt is retained, including empty, invalid, unknown and repeated declarations. |
| `Header` | An exact standalone heading or introductory phrase listed in the catalog. Some phrases can wrap across lines. A heading declared unversioned in the catalog has a null identifier. |
| `FullText` | A complete body whose normalized SHA-256 matches a catalog entry. A heading retains its own range and identifier independently. |

Full-text recognition accepts a standalone body after optional front matter drawn from the following list. The body must continue to the end of the document except for trailing ASCII whitespace.

- Recognized headings and leading explicit SPDX declarations. A heading and a complete normative body can identify different licenses. Both entries retain their own source text.
- Single lines beginning with `Copyright ` (case-insensitive) or `© ` followed by a value.
- License-specific front matter declared in the catalog. For example, OFL accepts its FAQ lines and hyphen separators, and MIT accepts `All rights reserved.` after a copyright line. If multiple license-specific lines occur, the matched body must belong to a profile that admits all of them. [Microsoft MIT heading and copyright example](https://github.com/dotnet/dotnet/blob/main/LICENSE.TXT)

To compute the SHA-256 of a body, trim surrounding ASCII space, tab, CR and LF, replace each internal run of those characters with one ASCII space, and map curly double quotes to ASCII double quotes. Hash the resulting UTF-8 bytes without BOM. The comparison preserves case and all other punctuation. Bodies longer than 16,384 UTF-16 code units after normalization do not produce a full-text match. A modified, truncated or supplemented body can still yield heading or explicit declaration evidence. A filename or incidental mention contributes no evidence.

The embedded catalog covers selected published bodies for `MIT`, `OFL-1.1`, `OFL-1.0`, `Ubuntu-font-1.0`, `Apache-2.0`, `BSD-2-Clause`, `BSD-3-Clause`, `ISC` and `0BSD`. Coverage is limited to fingerprints in the catalog. OFL 1.1 accepts the SIL double-hyphen and SPDX em-dash variants. Apache-2.0 accepts the official terms with or without the publisher's exact appendix. When present in a recognized document, that appendix is included in the full-text range. [Embedded catalog](../Font.Metadata.Spdx/LicenseProfiles.json) · [SIL plaintext](https://openfontlicense.org/documents/OFL.txt) · [SPDX OFL 1.1](https://spdx.org/licenses/OFL-1.1.html) · [SPDX MIT](https://spdx.org/licenses/MIT.html) · [Apache-2.0 plaintext](https://www.apache.org/licenses/LICENSE-2.0.txt)

### Caller-supplied catalogs

Pass an immutable `FontLicenseDocumentCatalog` in `FontLicenseDocumentOptions.Catalog` for a single call. `Parse(string json, bool includeBuiltIn = false)` accepts a JSON array of profiles. By default the array replaces the built-in catalog. Passing `includeBuiltIn: true` adds the supplied profiles alongside the built-ins with collision checks. An empty replacement array leaves only explicit SPDX declaration recognition. The supplied `SpdxLicenseRegistry` continues to validate those declarations. Profile identifiers label text matches under the caller's catalog and there is no registry validation of those match labels.

```csharp
using System;
using System.Security.Cryptography;
using System.Text;
using Fonts.Metadata;
using Spdx.Expressions;

const string body = "Example permission. Example conditions.";
string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
string profiles = $$"""
[
  {
    "identifier": "LicenseRef-Example",
    "headings": [{ "match": "wholeLineIgnoreCase", "text": "Example Terms" }],
    "bodySha256": ["{{digest}}"]
  }
]
""";
var options = new FontLicenseDocumentOptions {
    Catalog = FontLicenseDocumentCatalog.Parse(profiles, includeBuiltIn: true)
};
var result = FontLicenseDocumentAnalyzer.Analyze("Example Terms\n" + body, SpdxLicenseRegistry.Default, options);
```

Each profile requires `identifier` and `bodySha256`. The optional `headings` and `frontMatter` arrays default to empty. The optional `source` is an HTTP(S) reference for maintainers and is never fetched. Use ASCII letters, digits, hyphens and dots for identifiers, starting with a letter or digit. Use one to sixteen 64-digit hexadecimal SHA-256 strings per profile.

| Rule `match` | Meaning |
| --- | --- |
| `wholeLine` or `wholeLineIgnoreCase` | Compare the whole trimmed line with `text`, using ordinal case-sensitive or ordinal case-insensitive matching. |
| `wrappedPhrase` | Compare the whole phrase case-insensitively. Use single spaces in `text` where input spaces, tabs or line breaks may occur. |
| `prefixWithValue` | Require a case-insensitive leading `text` and at least one character after it. |
| `prefixWithExactSuffix` | Require a case-insensitive leading `text` followed by the exact ordinal `suffix`, allowing intervening ASCII spaces. |
| `separator` | Require a trimmed line of repetitions of the single character in `text`, with at least `minimumLength` occurrences (3 through 256). |

All rules require `match` and `text`. Headings support the first two rows and can set `unversioned: true` to report a null identifier. A matching heading or wrapped phrase retains the longest recognized source range. An explicit SPDX declaration line retains its own evidence even next to a wrapped phrase. Front matter can set `after: "copyright"` to require a preceding copyright line, skipping blank lines and explicit declarations. Identifiers may be up to 128 characters and rule text and suffixes may be up to 256 characters. Parsing accepts up to 262,144 UTF-16 code units of JSON, 64 profiles and 256 rules, including built-ins when merging. Headings that can match the same text must report the same identifier. Two `wholeLine` headings can distinguish `Terms` from `TERMS`. Using `wholeLineIgnoreCase` for either heading makes that pair overlap. Invalid, duplicate or ambiguous definitions throw `JsonException`. Parsing is offline and returns an immutable catalog that callers can reuse.

### Document limits and diagnostics

| Limit | Default | Behavior |
| --- | ---: | --- |
| `MaxSourceCharacters` | 1,048,576 | Rejects oversized strings before recognizing any evidence. Returns `FDL_SOURCE_CHARACTER_LIMIT` with null coordinates. |
| `MaxEvidence` | 256 | Keeps earlier entries in order and stops at the first omitted entry. Returns `FDL_EVIDENCE_LIMIT` with that entry's coordinates. |
| Native SPDX limits | Pinned parser and validator defaults | A parser limit is retained at its declaration and analysis continues. Validation diagnostic truncation is reported by the native result, whose occurrences are preserved. |

`IsAnalysisComplete` is false on a document or parser limit. Syntax errors, unknown identifiers and unresolved references remain evidence in an otherwise complete analysis. Native diagnostic truncation is separately visible on the declaration. See the [CLI document command](cli.md#external-license-documents) for the byte-read, digest and UTF-8 contract.

## Headers and table inventory

```csharp
var inspection = FontMetadataReader.InspectFile(path, new FontReadOptions { IncludeTableInventory = true });
foreach (var face in inspection.Faces) {
    if (face.Head is { } header)
        Console.WriteLine($"{header.FontRevision / 65536m} ({header.UnitsPerEm} units per em)");
    Console.WriteLine($"{face.MaxpTableStatus} {face.GlyphCount}");
    foreach (var table in face.TableInventory!.Entries)
        Console.WriteLine($"{table.Tag} {table.Length} {table.StoredLength}");
}
```

`HeadTableStatus` and `MaxpTableStatus` describe independent reads. `Head` is null when no fields are available. `Head.FontRevision` preserves signed 16.16 bits (`0xffffffff` gives -1). `Created` and `Modified` are signed 64-bit seconds since 1904-01-01 UTC, including values outside the .NET date range. `UnitsPerEm`, `Flags`, `MacStyle` and `GlyphCount` preserve unsigned values including zero. Font version selection and rounding belong to the caller.

`TableInventory` is null by default. When enabled, its status and immutable entries describe the directory of each face in source order. Tags preserve all four bytes. Lengths are bytes. SFNT and TTC use the declared table length for both fields. WOFF reports original and individually stored lengths with `IsCompressed`. WOFF2 reports original length and the table's contribution before shared Brotli compression, including zero for a transformed loca. `TransformVersion` preserves the two flag bits and `IsTransformed` accounts for the null transform for the specific tag. EOT lengths describe the contained SFNT or decoded CTF. A directory entry records a declaration. Signature verification, hint validation and outline reconstruction are separate operations.
