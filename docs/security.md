# Processing untrusted fonts

Font files can originate from uploads, documents, archives or external repositories. Font.Metadata checks the structures it reads and provides resource limits, cancellation and explicit incomplete results. These mechanisms support handling untrusted input within the documented metadata scope; the library is not a process sandbox or a complete font validator.

## Input and parsing boundaries

File, stream and memory entry points use the same container-reading pipeline. Recognition starts at the supplied input origin rather than trusting the file extension. The reader checks sizes, arithmetic and offsets before accessing supported structures or allocating controlled buffers.

Recovery uses verified ranges. An error in one table or face can preserve independently readable metadata, including separate EOT header, main and EUDC facts. Callers should inspect overall and per-section statuses as well as diagnostic codes. A partial result is not interchangeable with a complete result.

Checks have a defined scope. TTC directory protection covers inspected faces; the reader does not validate every relationship between tables in different faces. Glyph programs, full outline validity, SFNT table checksums and cryptographic authentication are outside its validation contract. See [Formats and boundaries](formats.md#validation-boundaries).

## Resource budgets

Configure `FontReadOptions.Limits` for the application's workload. The [API limit reference](api.md#limits) lists defaults and exact accounting rules.

| Boundary | Existing controls |
| --- | --- |
| Input and retained tables | `MaxInputBytes`, `MaxTableBytes` |
| Collection and record traversal | `MaxFaces`, `MaxTablesPerFace`, `MaxNameRecords` |
| String decoding and diagnostics | Per-table and total string budgets, `MaxDiagnostics` |
| Decompression work | `MaxTotalDecodedBytes`, `MaxMtxPayloadBytes`, `MaxExpansionRatio` |
| WOFF2 structure | `MaxWoff2Tables`, `MaxWoff2TableReferences` |
| Controlled decoded buffers | `MaxRetainedDecodedBytes` |

Per-face and per-table limits retain their documented scope; they are not a single global count of all objects. Total budgets remain shared across faces and EOT main/EUDC within one read operation. Recovery does not reset consumed work, and failed decode attempts are counted. Exact shared metadata can be reused within the operation.

Input size alone does not bound decompression. WOFF checks selected zlib streams; WOFF2 streams Brotli output and retains selected metadata; MTX accounts for all decoded streams and optional RLE work. Container checks and decompression budgets remain separate from the size of the original input.

These controls do not impose an exact managed-heap or process-memory ceiling. Returned strings and records, caller input, stream buffering, runtime objects and codec internals have costs beyond controlled decoded-buffer capacity. Limits apply per operation; applications also control concurrency and how long results remain referenced.

A limit diagnostic is an incomplete-processing outcome, not evidence that the entire input was inspected. Diagnostic suppression is explicit when the diagnostic budget is exhausted.

## Cancellation and process isolation

Cancellation is cooperative during reading and decoding. A token does not enforce a hard deadline or interrupt a synchronous `Stream.Read` that is already blocked. The CLI uses asynchronous input handling for binary stdin, but cancellation still depends on the behavior of the underlying I/O operations. See [CLI cancellation](cli.md) and the [API contract](api.md).

Applications that require enforced time or memory limits should run inspection in a separate worker process, apply operating-system limits and terminate the process tree when a deadline is exceeded. Cancellation and library budgets complement those controls; they do not replace them.

Expected malformed-data failures are represented through results and diagnostics. I/O errors, cancellation, programming errors and out-of-memory failures are not all converted into malformed-font results. Callers should handle the documented exceptions and preserve the distinction between data failures and execution failures.

## Output and license evidence

Decoded names, URLs, license strings and diagnostic context remain untrusted text. Preserve their provenance and encode them appropriately when displaying them in HTML, terminals or other output formats. Recognition of a URL does not make it safe to fetch.

The CLI escapes input-controlled text in plain reports and uses JSON escaping for machine-readable output. It does not follow metadata URLs or create terminal hyperlinks. Applications using the library own their presentation and any subsequent network access.

License analysis is a separate operation on decoded name records. It performs no network or file access and does not establish ownership, license authenticity, or permission to use or redistribute a font. See [License analysis](licensing.md).

`Complete` means complete extraction of supported metadata. It is not a trust decision, an authenticity check, or approval to pass the file to a renderer.

## Verification scope

Repository tests cover malformed fixtures, deterministic mutations, recovery, resource limits, allocation regressions and cancellation. Package checks exercise the distributed API and CLI artifacts. These checks do not establish exhaustive fuzzing coverage or an independent security audit.

Independent validation of 45 reviewed samples, including scoped FontTools/OTS comparisons, completed without confirmed product defects. A separate bounded Windows campaign passed 62 reviewed contract/boundary checks and observed no execution failures in 256 deterministic mutations under an enforced Job Object memory cap and timeout. Job isolation was also tested with controlled parent/child allocation and termination failures. These are single-host observations, not exhaustive or coverage-guided fuzzing; the resource controls do not isolate filesystem or network authority and the results do not establish security or performance guarantees. The initial research cycle is complete with these limitations. Further checks are triggered by releases or relevant changes; see [Testing](testing.md#independent-validation) for the evaluated package, environment and coverage limits.
