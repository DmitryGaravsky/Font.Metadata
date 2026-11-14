# SPDX expression analysis

`Font.Metadata.Spdx` is an optional .NET 10 package in namespace `Fonts.Metadata`.
It depends on `Font.Metadata` and the exact `Spdx.Expressions` version `0.1.0`.
The reader itself has no SPDX dependency. The CLI bundles the adapter and initializes
the offline registry only with `--analyze-spdx`.

The dependency is published as [Spdx.Expressions 0.1.0](https://www.nuget.org/packages/Spdx.Expressions/0.1.0).
Local and CI builds restore it from nuget.org; no adjacent source checkout or supplied local
package is required. Runtime analysis remains offline. Publishing Font.Metadata packages is
separate from restoring this dependency.

## API

```csharp
using System;
using Fonts.Metadata;
using Spdx.Expressions;

var inspection = FontMetadataReader.InspectFile("example.ttf");
var registry = SpdxLicenseRegistry.Default;
foreach(var face in inspection.Faces) {
    FontSpdxAnalysis analysis = FontSpdxAnalyzer.Analyze(face, registry);
    foreach(var declaration in analysis.Declarations) {
        Console.WriteLine($"{declaration.RuleId}: {declaration.ExpressionText}");
        Console.WriteLine(declaration.ParseResult.Status);
        // ParseResult.Expression exposes the native immutable AST on success.
        // ValidationResult is null on syntax or parser-limit failure.
    }
}
```

`Analyze(FontMetadata.Face face, SpdxLicenseRegistry registry, FontSpdxOptions? options = null,
CancellationToken cancellationToken = default)` performs no I/O and does not change metadata,
reader diagnostics or legacy `FontLicenseAnalyzer` hints. Null outer options selects defaults;
null face, registry or nested options throws `ArgumentNullException`. Invalid limits throw
`ArgumentOutOfRangeException`, including for empty faces. Cancellation throws
`OperationCanceledException` with no partial result. Unexpected runtime faults propagate.

Results are sealed, immutable and internally constructed. `FontSpdxAnalysis` exposes
`RecognitionProfile` (`font-name13-v1`), `SyntaxProfile` (`SPDX-2.3.1`), `RegistryIdentity`,
`Declarations`, adapter `Diagnostics` and `DiagnosticsTruncated`, plus two independent booleans:

- `IsSourceComplete`: the name table is Complete or Missing. Partial/Invalid/Unsupported/NotRead is false.
- `IsAnalysisComplete`: the available records were processed without work or result exhaustion.
  Invalid explicit syntax is a completed attempt. Validator message truncation preserves
  complete traversal and does not make the analysis incomplete.

Each `FontSpdxDeclaration` exposes `RuleId`, the original `SourceRecord` object,
`SourceStart`, `SourceLength`, exact `ExpressionText`, native `ParseResult` and nullable
native `ValidationResult`. The AST preserves grouping, order, spelling and plus suffixes.
Validation retains every identifier/reference occurrence and independent error/reference/
message-truncation flags. References remain unresolved. No expression is selected as a final
license and no AND/OR relationship is invented between source records.

## Recognition boundaries

Only name ID 13 is examined, including nonpreferred languages and repeated records in source
order. ID 14 URLs remain part of legacy hint analysis. Trim only outer ASCII space, TAB, CR
and LF; never normalize casing, NUL, NBSP, punctuation or quotes.

| Source | Result |
| --- | --- |
| `MIT OR Apache-2.0` | FSI002, one expression; no change to legacy hints |
| `SPDX-License-Identifier: MIT OR` | FSI001, invalid syntax, null validation |
| `SPDX-License-Identifier: Unknown-1.0` | FSI001, successful syntax with identifier error |
| `MIT OR Unknown-1.0` | FSI002, successful syntax with identifier error |
| `LicenseRef-Custom` | FSI002, unresolved reference |
| `Unknown-1.0`, `MIT OR`, `Licensed under MIT` | No admitted bare declaration |
| `Classpath-exception-2.0` | No bare declaration; explicit annotation exposes wrong-kind evidence |
| `SPDX-License-Identifier: MIT` followed by a license body | The entire suffix is parsed; no first-line shortcut |

FSI001 requires the exact ordinal prefix at the start of the trimmed record. Parse the entire
remaining ASCII-trimmed suffix, even if empty, invalid or too large for the parser. An empty
suffix has a zero-length range immediately after the colon. FSI002 requires a successful
whole-record parse and at least one known license-role identifier or reference. All-unknown
bare expressions and invalid bare text are intentional exclusions. A bare parser limit cannot
prove nonrecognition and is reported as incomplete work. Catalogue changes can change bare
admission; review evidence when upgrading the pinned dependency.

All coordinates are UTF-16 units. Native spans are relative to `ExpressionText`; add
`SourceStart` to locate them in `SourceRecord.Value`, never a file byte offset. For
`  SPDX-License-Identifier: MIT OR Apache-2.0  `, start is 27 and length is 17; Apache-2.0 has
native start 7 and original-record start 34. EOF diagnostics may have length zero.

## Limits

| FontSpdxOptions member | Default |
| --- | ---: |
| MaxSourceRecords | 4096 |
| MaxTotalSourceCharacters | 1048576 |
| MaxDeclarations | 256 |
| MaxDiagnostics | 128 |
| ParseOptions.MaxInputLength | 65536 |
| ParseOptions.MaxIdentifierLength | 256 |
| ParseOptions.MaxTokens | 16384 |
| ParseOptions.MaxNodes | 8192 |
| ParseOptions.MaxParenthesisDepth | 64 |
| ValidationOptions.MaxDiagnostics | 128 |

All limits are positive; parenthesis depth is at most 256. Limits are per Analyze call.
Charge every visited record, then the full length of each ID 13 value (including rejected,
blank or repeated strings) before trimming or copying. Source budget exhaustion stops before
that record. Native parsing/validation precedes admission, which precedes result retention.
After MaxDeclarations is reached, bounded probes continue until the first additional admitted
attempt; blanks/rejected text alone do not cause false incompleteness. No expression is truncated.

Adapter diagnostics preserve their original triggering record:

| Code | Meaning |
| --- | --- |
| FSI_SOURCE_RECORD_LIMIT | First unprocessed record exceeds record budget |
| FSI_SOURCE_CHARACTER_LIMIT | First unprocessed ID 13 record exceeds aggregate character budget |
| FSI_DECLARATION_LIMIT | First admitted attempt omitted by result cap; later work stops |
| FSI_PROBE_PARSE_LIMIT | Bare probe exhausted native parsing; later bounded records can still be processed |

Explicit parse limits stay in native diagnostics. Adapter message storage keeps the first
MaxDiagnostics messages; omission sets DiagnosticsTruncated without hiding incompleteness.
Validator diagnostic truncation does not remove occurrences or hide late errors/references.
Cancellation is checked during scanning/trimming and passed to native Parse/Validate. Limits
bound work, not exact managed heap, wall time or already decoded source storage.

## CLI and JSON

```shell
font-metadata inspect Example.ttf --format json --analyze-spdx
font-metadata inspect One.ttf Two.woff2 --analyze-licenses --analyze-spdx
```

Both analysis flags and `--include-name-records` are independent. Commands without the new
flag retain their prior JSON/plain shape. The first CLI version uses fixed default analysis
limits. Expression errors and limits do not change read/I/O exit codes; inspect the analysis
fields even when exit is zero. Dependency initialization occurs before input/reporting and
outside I/O classification; a damaged installation has no promised recovery or special exit code.

Schema version remains 1. When enabled, `options.analyzeSpdx` is present and true; root
`spdxContext` contains recognition/syntax profiles, the actual registry version/hashes and all
effective options. Each available face, including placeholders, gains `spdxAnalysis` containing
the two completeness flags, declarations, adapter diagnostics and their truncation flag. Null
metadata or empty face arrays still have root context but no fabricated analyses.

Declarations project sourceRecord, ruleId, sourceStart/sourceLength, expressionText,
parseStatus (`success`/`invalidSyntax`/`limitExceeded`), parseDiagnostics and validation.
Success has no parse diagnostics and nonnull validation; failure has one diagnostic and null
validation. FSI002 is always successful. Diagnostics project code, severity, span and message.
Validation contains identifierOccurrences, references, diagnostics and its three independent flags.
Known identifiers have canonicalIdentifier and boolean isDeprecated; unknown/wrongKind have
both null. References contain nullable documentId, licenseId and the full-reference span.
Adapter messages project code, message and nonnull sourceRecord. All nullable fields are present;
arrays are never null. Source evidence is included even without --include-name-records.

The [schema](cli.schema.json) checks structure and conditional presence. Substring/range,
provenance and registry authenticity require semantic checks. Consumers must tolerate future
optional fields. JSON deliberately contains neither an AST nor a second formatted expression;
consumers can parse the exact expressionText when they need a tree. See the
[generated example](examples/cli-spdx.json).

## Build and verification

The central dependency range is `[0.1.0]`, so restore cannot silently select another version.
Use the repository's public NuGet configuration:

```powershell
./eng/verify.ps1
```

Verification writes packages to a new `artifacts/packages/<version>/<run-id>/` directory
(printed on success), then checks actual package consumers, the installed tool and portable
payload in fresh consumer caches. The closed consumer feed contains the exact SPDX artifact
selected by restore. Checks compare its DLL bytes with the consumer/portable copies and verify
its notices. Repository and user feed configurations are not changed. Build/test CI uses the
same command, without a producer checkout or build.

The tool includes the SPDX implementation license and upstream notices in
`notices/Spdx.Expressions/`. Catalogue identity remains distinct from package version.
Hosted checks must actually pass; local verification and workflow configuration alone do not
establish platform verification. Verification never publishes packages or changes Git state.
