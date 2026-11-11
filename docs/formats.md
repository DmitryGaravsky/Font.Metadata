# Formats and metadata

## Implemented readers

| Container | Recognized signature/version | Metadata extracted |
| --- | --- | --- |
| SFNT | 0x00010000, OTTO, true, typ1 | One face; name and OS/2 |
| TTC/OTC | ttcf, version 1.0 or 2.0 | Faces in source order; shared table support |
| WOFF | wOFF | Recognized, Unsupported |
| WOFF2 | wOF2 | Recognized, Unsupported |
| EOT | Known version at byte 8 and magic 0x504C at byte 34, little-endian | Recognized, Unsupported |

Recognition only identifies a candidate container; a recognizable truncated header may fail parsing. Inputs without a recognized leading signature return `NotRecognized`. File extensions do not select a parser.

A directory must be nonempty and fit the input. Tables must fit the input and must not overlap the container header, inspected face directories or other tables within their own face. Duplicate tags are ambiguous and cannot supply metadata. Unsorted unique tags and unaligned offsets produce warnings but remain readable when their ranges are safe.

TTC table offsets are absolute from the collection origin. Versions 1 and 2 have different header sizes; version 2 requires space for its DSIG descriptor. Exact shared metadata ranges are parsed once and their diagnostics are attributed to each referencing face. Directory protection covers recognized SFNT directories among the faces inspected within the configured limit. An unsupported face flavor cannot establish a directory layout or reserve unrelated table bytes.

## Name tables

Formats 0 and 1 are supported. Decoded records retain source order, source indexes, IDs, duplicate entries, unknown name IDs, empty strings, whitespace and NUL characters. Unsupported encodings and invalid byte sequences are diagnosed rather than guessed as ASCII.

| Platform | Encoding IDs | Decoder |
| --- | --- | --- |
| Unicode (0) | 0–6 | Strict big-endian UTF-16 |
| Windows (3) | 0, 1, 10 | Strict big-endian UTF-16 |
| Macintosh (1) | 0 | MacRoman, code page 10000 |
| Windows (3) | 2, 3, 4, 5, 6 | Code pages 932, 936, 950, 949, 1361 |
| ISO (2) | 0, 1, 2 | ASCII, big-endian UTF-16, Latin-1 |

Legacy code pages use the .NET encoding provider directly; the library never registers a global provider. Format-1 language strings are range-checked; referenced strings are decoded and exposed through `LanguageTag`. Unused language strings are checked for bounds and even UTF-16 length but are not decoded or included in string budgets.

Preferred values are selected per name ID:

1. Windows en-US records (language ID 0x0409 or language tag en-US).
2. Other Unicode or Windows records.
3. Macintosh records.
4. Other supported records.

Records with an unavailable or undecodable required language tag remain in NameRecords but are excluded from preferred-value selection, including when a string budget prevented decoding the tag. Equal-ranked eligible records retain source order. Selection is independent of process culture. Leading/trailing whitespace and NULs are removed only from preferred values; blank candidates are skipped. Raw `NameRecord.Value` is unchanged.

`FamilyName` prefers typographic name ID 16 over legacy ID 1; `SubfamilyName` prefers 17 over 2. Other preferred properties map to IDs 0, 4–14 as described by the OpenType name specification. All other decoded IDs remain accessible in `NameRecords`.

## OS/2 metadata

Versions 0–5 share the supported prefix:

| Field | Offset | Size | Result |
| --- | ---: | ---: | --- |
| fsType | 8 | 2 bytes | Raw unsigned embedding flags |
| achVendID | 58 | 4 bytes | ASCII vendor identifier |

A readable fsType survives a truncated or malformed vendor identifier. Vendor bytes must be printable ASCII or NUL; preferred vendor values trim leading/trailing whitespace and NULs. Blank vendor values become null. Unsupported versions return an explicit section status.

`Os2TableStatus.Complete` means that the supported 62-byte metadata prefix was readable. The reader does not require or validate later metrics, whose lengths vary by version. It does not interpret fsType as a license verdict.

## Validation boundaries

This is a metadata reader, not a rasterizer or comprehensive OpenType validator. It accepts metadata-only synthetic fonts and does not require head, cmap, glyph outlines or a family string. It does not validate glyph programs, checksums, searchRange fields, DSIG signatures or full OS/2 metric layouts. It does not validate all table relationships between different TTC faces, beyond inspected directory protection and exact shared metadata reuse.

`Complete` therefore does not certify a font for rendering, embedding, licensing or trust. Missing supported tables can be represented accurately by a complete result. Truncation, unsupported metadata features and configured limits are distinguished through [statuses and diagnostics](diagnostics.md).

## Specification references

- [OpenType font file and TTC structure](https://learn.microsoft.com/en-us/typography/opentype/spec/otff)
- [Naming table](https://learn.microsoft.com/en-us/typography/opentype/spec/name)
- [OS/2 table](https://learn.microsoft.com/en-us/typography/opentype/spec/os2)
