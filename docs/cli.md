# CLI inspection

Package: `Font.Metadata.Tool`. Command: `font-metadata`. Requires .NET 10.

For installation and task-oriented examples, see [CLI recipes](cli-recipes.md).

## Commands

```text
font-metadata
font-metadata --help
font-metadata -h
font-metadata --version
font-metadata inspect --help
font-metadata inspect -h
font-metadata inspect <path> [<path> ...] [options]
font-metadata inspect - [options]
font-metadata inspect [options] -- <literal-path> [<literal-path> ...]
font-metadata inspect-license --help
font-metadata inspect-license <path> [<path> ...] [--format plain|json]
```

Inspection is explicit; a bare filename is not a command. No arguments display help and do not read redirected stdin. Commands, options and values are case-sensitive. Options may appear anywhere after `inspect`, before `--`.

| Option | Default | Meaning |
| --- | --- | --- |
| `--format plain\|json` | plain | Human-readable text or one versioned JSON report |
| `--include-name-records` | off | All available decoded name records, including unknown IDs and duplicates |
| `--analyze-licenses` | off | Per-face license indications with their complete source records |
| `--analyze-spdx` | off | Independent whole-record SPDX expression attempts, native outcomes and source evidence |
| `--include-unicode-coverage` | off | Per-face cmap selection, section status and inclusive Unicode scalar ranges |
| `--include-variation-axes` | declared tables only | Report fvar status for every face, including missing or unreadable directories |
| `--include-table-inventory` | off | Per-face directory status and entries with tags, lengths and transform information |
| `--max-input-bytes N` | 134217728 | Maximum bytes per source; other library decoding limits still apply |

Values must be separate arguments; `--format=json`, aliases, repeated options and mixed help/inspection requests are invalid. N is ASCII decimal digits without signs, separators or suffixes, in 1..Array.MaxLength-1. Leading zeros are allowed. At least one source is required. All arguments are validated before any input is opened.

Before `--`, `-` is binary stdin and must be the sole source. After `--`, every token is a literal path, including `-` or `--help`. Use `--` or a `./` prefix for filenames beginning with a dash. Empty/whitespace-only paths and NUL are invalid arguments.

The tool preserves input order and repeated paths. It does not expand wildcards, environment variables or `~`, traverse directories, open URLs, extract archives/resources, or search inside opaque bytes. It inspects the beginning of each input regardless of extension. Directory/open failures become per-source I/O results.

```shell
font-metadata inspect Example.ttf
font-metadata inspect One.ttf Two.woff2 --format json
font-metadata inspect Variable.ttf --include-unicode-coverage --include-variation-axes --format json
font-metadata inspect Example.eot --include-name-records --analyze-licenses
font-metadata inspect - --format json
font-metadata inspect --format plain -- -font.ttf
font-metadata inspect Example.woff2 --max-input-bytes 67108864
```

Use a binary-preserving pipe for stdin. It is never read through a text decoder. The tool reads up to EOF or one byte beyond the input limit; exceeding the limit returns the library's incomplete result instead of parsing a truncated prefix. Reader processing budgets reset between files, but remain shared across faces and EOT main/EUDC within one container. See [limits](api.md#limits). Optional SPDX analysis has separate budgets that reset for each face; see [SPDX limits](spdx.md).

## Reports and channels

stdout contains only the selected report. Expected data failures and source I/O errors are included in that report; stderr remains empty. Processing continues after an individual source fails. Partial metadata, placeholder faces, original indexes, section states and diagnostics are preserved.

stderr is reserved for `CLI_INVALID_ARGUMENTS` (plus usage), `CLI_CANCELLED`, and `CLI_OUTPUT_ERROR`. Argument errors produce no report, even with `--format json`. Output/flush failure stops the batch. Messages are intended for people and are not stable machine-readable contracts; codes and fields are.

Plain reports show source, status, format, EOT header facts, faces, preferred fields, raw flags and diagnostics. Missing preferred strings may be omitted; table statuses always remain visible. Null flags differ from verified zero. uint flags/flavors use eight hex digits, embedding flags four; offsets and indexes are decimal. Input strings, paths and messages are quoted with JSON-style escaping, including terminal controls and bidi characters. URLs are never opened or emitted as terminal hyperlinks. Formatting is for people, not a parsing contract.

`Complete` means complete supported metadata, not full font validation or permission to use a font. EOT header facts never replace face facts. Byte offsets remain relative to the input origin; compressed diagnostics may point to the compressed-block anchor. `tableByteOffset` is a separate logical table coordinate and must not be added to that anchor.

## JSON contract, schema version 1

The [complete report with table inventory](examples/cli-complete.json) and the [EOT recovery report](examples/cli-eot-recovery.json) show the output of 1.1.0. The [1.0.0 report for the same EOT input](examples/cli-eot-recovery-1.0.0.json) illustrates schema-v1 compatibility. Package smoke tests validate all examples against the [JSON Schema](cli.schema.json).

Output is one UTF-8 JSON object without BOM, indented by two spaces and terminated by a newline, even for multiple inputs. No banners, timestamps, stack traces or resolved absolute paths are added. Property order is deterministic for readability; consumers must not depend on it.

Ordinary `inspect` includes `variationAxes` in plain and JSON output for each face whose directory identifies an fvar entry. This includes an empty, malformed, unsupported or budget-limited table. Faces without an entry and faces whose directory could not be inspected omit the section. With `--include-variation-axes`, every reported face has a section, so a caller can distinguish proven absence (`missing`) from an unavailable directory or payload. The JSON option `includeVariationAxes: true` records the explicit audit flag only.

Unicode coverage is opt-in. With `--include-unicode-coverage`, JSON contains `includeUnicodeCoverage: true` and every reported face has a `unicodeCoverage` section. Each object carries its own `status` and `diagnostics`. Coverage reports the selected `platformId`, `encodingId`, `format` and inclusive integer ranges (`start`, `end`). Axes carry `index`, `tag`, `minimum`, `default`, `maximum`, `flags` and `nameId`. For localized axis labels, also request `--include-name-records`. Section-local errors are reported within the requested section. Existing container errors still affect the source status and exit code.

The CLI projects library results into its own wire format. Package versions and `schemaVersion` evolve independently. A v1 consumer should check the version, read the fields it uses by name, and ignore additional optional properties. Existing required fields, JSON types, nullable values, source indexes, ordering of results and option-dependent presence keep their documented meanings. A new opt-in analysis may add optional properties within v1 when existing invocations and their exit codes keep their behavior.

Removing or renaming an existing field, changing its type or meaning, or emitting a new value in an existing closed schema enum requires a new schema version. During 1.x, existing v1 invocations must remain available if a later format is introduced. An unfamiliar status must never be interpreted as a successful inspection. Recognition results may evolve with documented rule or catalog updates. Compare the profiles and registry identity when comparing SPDX reports.

| Root field | Meaning |
| --- | --- |
| schemaVersion | 1 |
| options | `includeNameRecords`, `analyzeLicenses` and `maxInputBytes` are always present. `analyzeSpdx`, `includeUnicodeCoverage`, `includeVariationAxes` and `includeTableInventory` are present and true only for their explicit flags. |
| spdxContext | Present only with --analyze-spdx: recognition/syntax profiles, registry identity and analysis limits |
| results | Nonempty array in argv order |

Each result has `source`, `status`, `metadata` and `diagnostics`. Source is `{ "kind": "file", "path": "<original argument>" }` or `{ "kind": "stdin", "path": null }`. Relative paths remain relative; a literal file named `-` is distinguishable from stdin.

Statuses: `complete`, `partial`, `failed`, `unsupported`, `notRecognized`, `ioError`. Metadata is the actual reader result or null, not a fabricated empty success. A recognized but damaged container may still have metadata. An oversized unrecognized input can be failed with null metadata.

Metadata has `format` (unknown/sfnt/collection/woff/woff2/eot), `eotHeaderStatus`, nullable `eot`, and `faces`. Section statuses are notRead/missing/complete/partial/invalid/unsupported. Non-EOT reports have eot=null and eotHeaderStatus=missing.

EOT fields, all nullable: formatVersion, flags (uint32), embeddingPermissions (uint16), familyName, styleName, fullName, version (strings), eudcCodePage, eudcFlags, eudcFontDataSize (uint32). Numeric formatVersion differs from the header's textual version. RootString and Signature are not exposed.

Every face has:

- index (original nonnegative index), source (container/eotMain/eotEudc), sfntFlavor (nullable uint32).
- `nameTableStatus`, `os2TableStatus`, `headTableStatus` and `maxpTableStatus`.
- `head` (nullable object of raw header fields) and `glyphCount` (nullable uint16).
- vendorId (nullable string), embeddingPermissions (nullable uint16 raw fsType).
- Nullable strings: familyName, subfamilyName, fullName, postScriptName, version, copyright, trademark, manufacturer, designer, description, vendorUrl, designerUrl, licenseDescription, licenseUrl.

All documented nullable properties are present as null; zero, false, empty string and null remain distinct. Arrays are never null. The following face properties are conditional.

- `variationAxes`, present for a declared fvar entry by default and for every face with `--include-variation-axes`.
- `tableInventory`, present for every face with `--include-table-inventory`, with its own status and ordered entries.
- `unicodeCoverage`, present for every face with `--include-unicode-coverage`.
- `nameRecords`, present with --include-name-records, including an empty array for unavailable records.
- `licenseAnalysis`, present with --analyze-licenses, including for placeholder faces.
- `spdxAnalysis`, present with --analyze-spdx, including for placeholder faces.

The opt-in SPDX extension also adds `options.analyzeSpdx=true` and root `spdxContext`; both are absent without the flag. See [SPDX API, JSON and limits](spdx.md) for the complete contract and [example](examples/cli-spdx.json). Parse/validation findings do not change reader exit codes. The default registry initializes before input/reporting; package faults are not reported as source/output I/O errors.

Name records contain index, nameId, platformId, encodingId, languageId, nullable languageTag and unmodified value. Source indexes may have gaps and are not array positions. Unknown name IDs, duplicates, whitespace and NULs survive JSON decoding unchanged.

Diagnostics contain code, severity (warning/error), category, message, and nullable faceIndex, tableTag, byteOffset, tableByteOffset. `FM_LIMIT_EXCEEDED` includes the reader setting in `limitReason`. Reader categories are malformedData/limitExceeded/unsupportedFeature/unsupportedEncoding/nonConformantData. Per-source I/O uses code CLI_IO_ERROR, category io, severity error, null context fields and metadata=null. The schema rejects empty result batches, metadata on ioError/notRecognized, and ioError without its diagnostic. OS-specific I/O message wording may vary. Reader diagnostics retain their original order and suppression marker.

## License evidence

Preferred licenseDescription/licenseUrl are always ordinary metadata. Full raw name records are available independently through --include-name-records.

With --analyze-licenses, each face gains `licenseAnalysis` with `isSourceComplete` and `hints`. Each hint contains identifier, identifierKind (spdx/family), ruleId and a complete sourceRecord. Evidence remains available even without --include-name-records. Repeating that record when both options are enabled is intentional.

Source completeness follows the name-table status, not the overall font or OS/2 state. A complete or missing name table can yield true and no hints. This does not prove absence of a license or complete recognition coverage. Conflicting indications remain visible. Family is not converted to an individual SPDX ID, and expressions are not synthesized or simplified.

The tool never derives ownership, approval or licensing permission from hints or fsType/EOT flags, follows links, or searches adjacent license files. Hints do not change exit status. See [license rules and boundaries](licensing.md).

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Help/version, or every input is complete; warnings are allowed |
| 1 | At least one notRecognized/partial/failed/unsupported result, without I/O failure |
| 2 | Invalid arguments; no source opened |
| 3 | A source I/O failure, or report output/flush failure |
| 130 | Observed cooperative cancellation |

Priority for a valid command: cancellation, I/O, read status, success. Missing optional tables or family names and empty/conflicting license hints do not themselves fail the command. A source I/O failure still produces a complete report and does not stop later files. An output failure, including a downstream consumer closing its pipe, can leave truncated stdout; stderr reports CLI_OUTPUT_ERROR when writable.

## Cancellation and memory

Ctrl+C requests cooperative cancellation. No new sources start after cancellation is observed. A cancelled streaming JSON report may be unfinished; do not consume it as a complete report. Forced process termination guarantees neither a controlled exit code nor complete output.

Stdin uses bounded asynchronous buffering so cancellable streams can interrupt waiting reads. Native stdin/file/output operations may not support immediate cancellation on every OS; the tool does not promise to interrupt an already blocked synchronous operation. Tests exercise cancellation on a real asynchronous pipe and deterministic orchestration seams; console signal delivery itself depends on the host.

Inputs and results are processed one at a time, without retaining a whole batch or building the entire JSON report as one string. Read limits bound work, not exact managed heap or report size. Large/repeated evidence can produce a large report; strings and records are not silently truncated.

## Running and distribution

```shell
dotnet run --project Font.Metadata.Tool -- inspect Example.ttf --format json
dotnet Font.Metadata.Tool.dll inspect Example.ttf --analyze-licenses
```

After publication, install the tool package with `dotnet tool install --global Font.Metadata.Tool --version <version>`, or use the portable ZIP with the .NET 10 runtime. Local verification installs into an isolated tool directory, tests actual inspection and validates JSON against the local schema. Nothing is published by local verification.

## External license documents

```shell
font-metadata inspect-license LICENSE OFL.txt NOTICE --format json
font-metadata inspect-license --format plain -- -literal-name
```

`inspect-license` processes only the given paths, in argument order, including repeats. `--format plain|json` is its only report option and plain is the default. Use `--` for literal paths beginning with a dash. A bare `-` before `--` is invalid. Arguments are validated before any path is opened. The command opens each file independently and continues after file errors. The report describes the supplied documents and carries no association to a font or to its NAME records.

The per-file limit is 1,048,576 bytes, including any BOM. Reading stops at EOF or one byte over the limit. SHA-256 is calculated over the original bytes on a full read, even if decoding or analysis subsequently fails. Decoding is strict UTF-8 with an optional initial UTF-8 BOM. A BOM contributes one U+FEFF to UTF-16 offsets and is excluded from the first fragment. Other encodings, including UTF-16 and UTF-32 BOM files, are unsupported. Embedded NUL and any further U+FEFF are ordinary decoded characters. Invalid UTF-8 yields no text evidence. See the [recognition profile and UTF-16 range rules](api.md#external-license-document-analysis).

Plain output includes the path, status, available digest, evidence origin and ranges, SPDX parse status, and diagnostics. Heading fragments and SPDX expressions are quoted and escaped. The normative body is represented by its identifier and range. JSON retains exact fragments.

The command uses the built-in license catalog. The catalog can gain recognized headings and full-text bodies as the package evolves. The shape of JSON v1 and the coordinate system remain the same. [Built-in coverage](api.md#recognition-profile)

| Exit | Meaning for `inspect-license` |
| --- | --- |
| 0 | Every file was read, decoded and analyzed within its limits. The report may contain no evidence, unknown identifiers, unresolved references or invalid expressions. |
| 1 | At least one input byte, document or SPDX parser limit, or invalid encoding. |
| 2 | Invalid arguments. No report is produced. |
| 3 | A file I/O or report output failure. File I/O takes precedence over exit 1. |
| 130 | Observed cancellation. A report in progress may be incomplete. |

Expected per-file failures appear on stdout in the report. Argument, cancellation and report output errors use the shared stderr codes documented above.

### License document JSON contract, schema version 1

The [license document JSON Schema](cli-license.schema.json) describes the separate `inspect-license` report. Output is one UTF-8 JSON object without BOM, followed by a newline. All fields below are present, arrays may be empty, and documented nullable fields have explicit null values. Read fields by name and tolerate additional optional fields.

| Root field | Contents |
| --- | --- |
| `schemaVersion` | `1` |
| `options` | `maxInputBytes`, `maxSourceCharacters`, `maxEvidence` |
| `spdxContext` | `recognitionProfile` (`license-document-v1`), `syntaxProfile` (`SPDX-2.3.1`), `registry` with `licenseListVersion`, `licensesSha256`, `exceptionsSha256` |
| `results` | Nonempty array of input results in argument order |

Each result contains:

| Field | Meaning |
| --- | --- |
| `source` | `kind` is `file`. `path` is the original argument. |
| `status` | `complete`, `limitExceeded`, `invalidEncoding` or `ioError` |
| `sha256` | Lowercase hexadecimal hash of all original bytes, or null for a byte limit or file I/O failure |
| `hasUtf8Bom` | True or false after a full read, null if the digest is unavailable |
| `evidence` | Source-ordered evidence entries. File errors, byte limits and invalid UTF-8 yield an empty array. |
| `diagnostics` | File and document-level diagnostics. Each has `code`, `message`, nullable `sourceStart` and nullable `sourceLength`. |

Document-level codes are `CLI_IO_ERROR`, `CLI_LICENSE_INPUT_LIMIT`, `CLI_LICENSE_INVALID_UTF8`, `FDL_SOURCE_CHARACTER_LIMIT` and `FDL_EVIDENCE_LIMIT`. A native parser limit appears in that evidence entry's `parseDiagnostics`.

| Evidence field | Meaning |
| --- | --- |
| `origin` | `spdxDeclaration`, `header` or `fullText` |
| `identifier` | A header or full-text identifier, or null for declarations and versionless headings |
| `sourceStart`, `sourceLength`, `sourceText` | Exact fragment coordinates and text in the decoded document |
| `expressionStart`, `expressionLength`, `expressionText` | Exact SPDX parser input and coordinates in the document, null for other origins |
| `parseStatus` | `success`, `invalidSyntax`, `limitExceeded` or null for other origins |
| `parseDiagnostics` | Native parse diagnostics, each with `code`, `severity`, `span` and `message` |
| `validation` | Native validation summary on parse success, null otherwise |

`validation` contains `hasIdentifierErrors`, `hasUnresolvedReferences`, `diagnosticsTruncated`, `identifierOccurrences`, `references` and `diagnostics`. An occurrence has `identifier`, `role` (`license` or `exception`), `lookupStatus` (`known`, `unknown` or `wrongKind`), nullable `canonicalIdentifier`, nullable `isDeprecated` and `span`. A reference has nullable `documentId`, `licenseId` and `span`. Validation diagnostics use the same fields as parse diagnostics. Native `span` objects have `start` and `length` in UTF-16 code units relative to `expressionText`. Add `expressionStart` to locate the span in the document.

## Header and directory output

`inspect` emits `headTableStatus`, `head`, `maxpTableStatus` and `glyphCount` per face. JSON uses signed integers for the raw revision and LONGDATETIME fields. Use an integer-preserving JSON reader for timestamps outside the precise integer range of binary64. Add `--include-table-inventory` to include `tableInventory.status` and its ordered `entries` with `tag`, `length`, `storedLength`, `isCompressed`, `isTransformed` and `transformVersion`. The JSON schema version remains 1. Diagnostics for exhausted reader budgets include `limitReason`.
