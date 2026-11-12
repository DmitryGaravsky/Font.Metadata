# CLI inspection

Package: `Font.Metadata.Tool`. Command: `font-metadata`. Requires .NET 10.

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
```

Inspection is explicit; a bare filename is not a command. No arguments display help and do not read redirected stdin. Commands, options and values are case-sensitive. Options may appear anywhere after `inspect`, before `--`.

| Option | Default | Meaning |
| --- | --- | --- |
| `--format plain\|json` | plain | Human-readable text or one versioned JSON report |
| `--include-name-records` | off | All available decoded name records, including unknown IDs and duplicates |
| `--analyze-licenses` | off | Per-face license indications with their complete source records |
| `--max-input-bytes N` | 134217728 | Maximum bytes per source; other library decoding limits still apply |

Values must be separate arguments; `--format=json`, aliases, repeated options and mixed help/inspection requests are invalid. N is ASCII decimal digits without signs, separators or suffixes, in 1..Array.MaxLength-1. Leading zeros are allowed. At least one source is required. All arguments are validated before any input is opened.

Before `--`, `-` is binary stdin and must be the sole source. After `--`, every token is a literal path, including `-` or `--help`. Use `--` or a `./` prefix for filenames beginning with a dash. Empty/whitespace-only paths and NUL are invalid arguments.

The tool preserves input order and repeated paths. It does not expand wildcards, environment variables or `~`, traverse directories, open URLs, extract archives/resources, or search inside opaque bytes. It inspects the beginning of each input regardless of extension. Directory/open failures become per-source I/O results.

```shell
font-metadata inspect Example.ttf
font-metadata inspect One.ttf Two.woff2 --format json
font-metadata inspect Example.eot --include-name-records --analyze-licenses
font-metadata inspect - --format json
font-metadata inspect --format plain -- -font.ttf
font-metadata inspect Example.woff2 --max-input-bytes 67108864
```

Use a binary-preserving pipe for stdin. It is never read through a text decoder. The tool reads up to EOF or one byte beyond the input limit; exceeding the limit returns the library's incomplete result instead of parsing a truncated prefix. Processing budgets reset between files, but remain shared across faces and EOT main/EUDC within one container. See [limits](api.md#limits).

## Reports and channels

stdout contains only the selected report. Expected data failures and source I/O errors are included in that report; stderr remains empty. Processing continues after an individual source fails. Partial metadata, placeholder faces, original indexes, section states and diagnostics are preserved.

stderr is reserved for `CLI_INVALID_ARGUMENTS` (plus usage), `CLI_CANCELLED`, and `CLI_OUTPUT_ERROR`. Argument errors produce no report, even with `--format json`. Output/flush failure stops the batch. Messages are intended for people and are not stable machine-readable contracts; codes and fields are.

Plain reports show source, status, format, EOT header facts, faces, preferred fields, raw flags and diagnostics. Missing preferred strings may be omitted; table statuses always remain visible. Null flags differ from verified zero. uint flags/flavors use eight hex digits, embedding flags four; offsets and indexes are decimal. Input strings, paths and messages are quoted with JSON-style escaping, including terminal controls and bidi characters. URLs are never opened or emitted as terminal hyperlinks. Formatting is for people, not a parsing contract.

`Complete` means complete supported metadata, not full font validation or permission to use a font. EOT header facts never replace face facts. Byte offsets remain relative to the input origin; compressed diagnostics may point to the compressed-block anchor. `tableByteOffset` is a separate logical table coordinate and must not be added to that anchor.

## JSON contract, schema version 1

See the machine-readable [JSON Schema](cli.schema.json), a [complete report](examples/cli-complete.json), and an [EOT recovery report](examples/cli-eot-recovery.json). Examples are validated with the package smoke tests.

Output is one UTF-8 JSON object without BOM, indented by two spaces and terminated by a newline, even for multiple inputs. No banners, timestamps, stack traces or resolved absolute paths are added. Property order is deterministic for readability; consumers must not depend on it.

The CLI uses an explicit projection, not automatic serialization of library objects. A breaking shape/type/meaning change requires a new schemaVersion. Consumers should tolerate new optional fields. Enum vocabulary changes require deliberate compatibility review. License recognition results may evolve independently of the report shape.

| Root field | Meaning |
| --- | --- |
| schemaVersion | 1 |
| options | includeNameRecords, analyzeLicenses (booleans), maxInputBytes (integer) |
| results | Nonempty array in argv order |

Each result has `source`, `status`, `metadata` and `diagnostics`. Source is `{ "kind": "file", "path": "<original argument>" }` or `{ "kind": "stdin", "path": null }`. Relative paths remain relative; a literal file named `-` is distinguishable from stdin.

Statuses: `complete`, `partial`, `failed`, `unsupported`, `notRecognized`, `ioError`. Metadata is the actual reader result or null, not a fabricated empty success. A recognized but damaged container may still have metadata. An oversized unrecognized input can be failed with null metadata.

Metadata has `format` (unknown/sfnt/collection/woff/woff2/eot), `eotHeaderStatus`, nullable `eot`, and `faces`. Section statuses are notRead/missing/complete/partial/invalid/unsupported. Non-EOT reports have eot=null and eotHeaderStatus=missing.

EOT fields, all nullable: formatVersion, flags (uint32), embeddingPermissions (uint16), familyName, styleName, fullName, version (strings), eudcCodePage, eudcFlags, eudcFontDataSize (uint32). Numeric formatVersion differs from the header's textual version. RootString and Signature are not exposed.

Every face has:

- index (original nonnegative index), source (container/eotMain/eotEudc), sfntFlavor (nullable uint32).
- nameTableStatus and os2TableStatus.
- vendorId (nullable string), embeddingPermissions (nullable uint16 raw fsType).
- Nullable strings: familyName, subfamilyName, fullName, postScriptName, version, copyright, trademark, manufacturer, designer, description, vendorUrl, designerUrl, licenseDescription, licenseUrl.

All documented nullable properties are present as null; zero, false, empty string and null remain distinct. Arrays are never null. The only conditional face properties are:

- `nameRecords`, present with --include-name-records, including an empty array for unavailable records.
- `licenseAnalysis`, present with --analyze-licenses, including for placeholder faces.

Name records contain index, nameId, platformId, encodingId, languageId, nullable languageTag and unmodified value. Source indexes may have gaps and are not array positions. Unknown name IDs, duplicates, whitespace and NULs survive JSON decoding unchanged.

Diagnostics contain code, severity (warning/error), category, message, and nullable faceIndex, tableTag, byteOffset, tableByteOffset. Reader categories are malformedData/limitExceeded/unsupportedFeature/unsupportedEncoding/nonConformantData. Per-source I/O uses code CLI_IO_ERROR, category io, severity error, null context fields and metadata=null. The schema rejects empty result batches, metadata on ioError/notRecognized, and ioError without its diagnostic. OS-specific I/O message wording may vary. Reader diagnostics retain their original order and suppression marker.

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
