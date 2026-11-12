# Statuses and diagnostics

## Overall status

| FontReadStatus | Meaning |
| --- | --- |
| NotRecognized | No recognized signature at the supplied origin; Metadata is null |
| Complete | All supported metadata was processed without loss; warnings may exist |
| Partial | Usable metadata survives malformed data, an unsupported feature or a limit |
| Failed | A data failure or resource limit prevented usable metadata extraction |
| Unsupported | A recognized format, version or encoding is unavailable, without usable metadata |

A recognized container normally supplies a `FontMetadata` object even if its Faces collection is empty. An oversized unrecognized input returns Failed with null Metadata because the input limit takes precedence over parsing. Container recognition or a placeholder face alone does not count as usable extracted metadata.

A readable table (including a valid empty name table) counts as processed metadata. A valid face directory proving both supported tables absent can also be Complete. Missing fields should always be interpreted alongside table statuses.

## Per-table status

Each face exposes `NameTableStatus` and `Os2TableStatus`.

| FontSectionStatus | Meaning |
| --- | --- |
| NotRead | Extraction did not start, or a processing budget prevented any values; absence is unknown |
| Missing | A complete directory proves that this tag is absent |
| Complete | All metadata supported by this reader was processed |
| Partial | Verified values survive incomplete processing |
| Invalid | Malformed data prevented extraction of verified values |
| Unsupported | An unsupported version or encoding prevented extraction of values |

A missing family name does not imply a missing name table. An unreadable directory produces NotRead, not Missing. OS/2 may be Partial with readable embedding flags and no vendor identifier. Missing is not an error by itself. Whole-file completeness is separate from each table's completeness: for example, an invalid unrelated table range can make inspection Partial while both metadata tables are Complete.

## Compressed recovery and EOT

An invalid WOFF compressed table publishes no decoded values. WOFF2 tables received completely before a later stream failure can be parsed, but Complete table status is downgraded to Partial; unfinished buffers remain Invalid or NotRead according to the cause. The same rule applies to metadata from a complete first MTX stream when a secondary stream fails. Proven Missing is not downgraded. License analysis consequently reports IsSourceComplete=false for salvaged name records.

An independent optional-block error does not downgrade a fully verified WOFF2 Brotli source. A conflicting bounded block limits the source to the preceding unambiguous prefix; completely received tables can survive as Partial.

EOT header completeness is independent of face statuses. A complete header can coexist with unsupported payloads; partially decoded headers preserve verified facts. Header diagnostics belong to the container, while main/EUDC diagnostics identify face 0/1. A shared WOFF2 stream failure has no face or table tag; a WOFF table failure identifies both.

## Diagnostic data

`FontDiagnostic` exposes:

- `Code`: stable machine-readable identifier, also available through `FontDiagnosticCodes`.
- `Severity`: Warning or Error.
- `Category`: MalformedData, LimitExceeded, UnsupportedFeature, UnsupportedEncoding or NonConformantData.
- `Message`: English explanation for humans; do not parse it for policy decisions.
- Nullable `FaceIndex`, `TableTag`, `ByteOffset`, `TableByteOffset`: available source context.

Offsets are relative to the supplied input origin, not the original file position of a slice or stream. Shared-table diagnostics are reported with each referencing face's original index. No caller file path or source bytes are inserted into messages.

TableByteOffset is the position inside a logical decoded metadata table, when known. Directory/framing and whole-stream errors do not invent a table offset.

| Storage | ByteOffset for a table error |
| --- | --- |
| Raw SFNT/TTC/WOFF | Actual position in the supplied input |
| Raw/XOR EOT | Payload start plus local SFNT table/error position |
| Zlib WOFF | Start of that compressed table |
| Brotli WOFF2 | Start of the common compressed font stream |
| MTX main/EUDC | Start of that payload's first LZCOMP stream (MTX header + 10) |

Decoded offsets are never added to a compressed anchor. Both coordinates remain unchanged when the same container is supplied through a nonzero array slice or a stream positioned after a prefix.

| Code | Typical cause |
| --- | --- |
| FM_INVALID_CONTAINER | Malformed WOFF/WOFF2 header, layout or optional block framing |
| FM_INVALID_COMPRESSED_DATA | Invalid/truncated stream, checksum, extra input/output or MTX/RLE failure |
| FM_INVALID_EOT_HEADER | Invalid EOT fields, strings, padding, root checksum or signature framing |
| FM_UNSUPPORTED_TRANSFORM | Unknown transformation version |
| FM_UNSUPPORTED_FLAGS | Unsupported EOT payload flags |
| FM_NONCONFORMANT_CONTAINER | Permitted deviation, such as nonzero WOFF2 reserved fields (Warning) |
| FM_INVALID_DIRECTORY | Truncated header/directory, invalid ranges, duplicate tags or overlaps |
| FM_INVALID_COLLECTION | Empty or truncated TTC header/offset array |
| FM_INVALID_NAME_TABLE | Invalid record range, byte sequence or language reference |
| FM_INVALID_OS2_TABLE | Truncated metadata prefix or invalid vendor bytes |
| FM_UNSUPPORTED_FORMAT | Recognized container or face flavor without a reader |
| FM_UNSUPPORTED_TABLE_VERSION | Unsupported TTC/WOFF2 collection, name or OS/2 version |
| FM_UNSUPPORTED_ENCODING | Unsupported name platform/encoding pair |
| FM_LIMIT_EXCEEDED | Configured processing budget exhausted |
| FM_NONCONFORMANT_DIRECTORY | Safe but unsorted or unaligned directory data |
| FM_DIAGNOSTICS_TRUNCATED | Additional diagnostics suppressed by MaxDiagnostics |

Diagnostic output is bounded. When it overflows, its last slot becomes a suppression marker; previously determined incomplete states are preserved. Even with MaxDiagnostics set to 1, an error cannot be silently converted into Complete. Diagnostic suppression conservatively makes the overall result incomplete, including when the suppressed messages were warnings.

Expected malformed-data failures produce results through Inspect. Invalid arguments, cancellation, I/O failures and unexpected runtime errors still throw. Read applies strict success semantics to the same result and retains it on the appropriate exception; see [API](api.md).
