# Formats and metadata

## Implemented readers

| Container | Recognized signature/version | Metadata extracted |
| --- | --- | --- |
| SFNT | 0x00010000, OTTO, true, typ1 | One face; name and OS/2 |
| TTC/OTC | ttcf, version 1.0 or 2.0 | Faces in source order; shared table support |
| WOFF | wOFF | Raw or zlib-compressed name and OS/2, independently |
| WOFF2 | wOF2 | Single fonts and collections; shared name/OS2 in a streamed Brotli payload |
| EOT | Versions 0x00010000, 0x00020001, 0x00020002 at byte 8; magic 0x504C at byte 34, little-endian | Separate header; main/EUDC SFNT/CTF, raw/XOR/MTX with LZCOMP and optional RLE |

Recognition only identifies a candidate container; a recognizable truncated header may fail parsing. Inputs without a recognized leading signature return `NotRecognized`. File extensions do not select a parser.

For SFNT/TTC, a directory must be nonempty and fit the input. Tables must fit the input and must not overlap the container header, inspected face directories or other tables within their own face. Duplicate tags are ambiguous and cannot supply metadata. Unsorted unique tags and unaligned offsets produce warnings but remain readable when their ranges are safe.

TTC table offsets are absolute from the collection origin. Versions 1 and 2 have different header sizes; version 2 requires space for its DSIG descriptor. Exact shared metadata ranges are parsed once and their diagnostics are attributed to each referencing face. Directory protection covers recognized SFNT directories among the faces inspected within the configured limit. An unsupported face flavor cannot establish a directory layout or reserve unrelated table bytes.

## Web containers

WOFF checks the directory, stored/original lengths, block ranges, alignment, padding and optional metadata/private block framing. All WOFF font tables, including the final one, require padding to the next four-byte boundary. Selected compressed tables must produce exactly their declared output and consume exactly one zlib member, including its checksum and trailer. A malformed compressed name table does not discard independent OS/2 data. Nonzero WOFF reserved fields are errors.

WOFF2 reads its global dictionary and collection descriptors with separate entry/reference budgets. UIntBase128 is canonical and overflow-checked; all permitted 255UInt16 representations are accepted. Nonzero reserved fields produce a warning, while totalSfntSize is advisory and never controls allocation. Known glyf/loca/hmtx transformations are accounted for in stored lengths and checked for directory dependencies; outlines are not reconstructed. Unknown transformations produce UnsupportedFeature diagnostics.

The entire Brotli stream is decoded and checked for exact input/output completion, even when name/OS2 are absent. Only selected metadata tables are retained. Completely received tables may survive a later stream failure as Partial; an unfinished table buffer is never parsed. Proven absence remains Missing. Errors in independent optional blocks leave fully validated metadata tables Complete while making the overall result Partial. A bounded optional block overlapping Brotli limits recovery to the unambiguous prefix. A broken bounded collection face does not remove independent siblings.

EOT header strings are strict UTF-16LE and remain separate from embedded names and fsType. Verified empty strings and numeric zeros remain distinct from unavailable fields. Root-string checksum and signature framing are checked; neither constitutes authentication. Valid header facts survive payload failures, and independently bounded main data can survive malformed variable header fields.

EOT main and EUDC have fixed indexes 0 and 1 and their own flags. XOR is removed before MTX decoding. All three MTX streams are validated; if the first CTF stream is complete but a later stream fails, its metadata can survive only as Partial. An incomplete first stream or RLE result is not parsed. Nested collections/containers and unknown payload flags or MTX versions are unsupported. CTF metadata uses the SFNT reader, which already permits an empty unrelated loca table.

Extended WOFF/WOFF2 XML and private block contents are not extracted or decoded. EOT root restrictions, signatures and outline reconstruction are outside the metadata API.

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

This is a metadata reader, not a rasterizer or comprehensive OpenType validator. It accepts metadata-only synthetic fonts and does not require head, cmap, glyph outlines or a family string. It does not validate glyph programs, SFNT table checksums, searchRange fields, DSIG signatures or full OS/2 metric layouts. It does not validate all table relationships between different TTC faces, beyond inspected directory protection and exact shared metadata reuse.

`Complete` therefore does not certify a font for rendering, embedding, licensing or trust. Missing supported tables can be represented accurately by a complete result. Truncation, unsupported metadata features and configured limits are distinguished through [statuses and diagnostics](diagnostics.md).

## Specification references

- [OpenType font file and TTC structure](https://learn.microsoft.com/en-us/typography/opentype/spec/otff)
- [WOFF](https://www.w3.org/TR/WOFF/)
- [WOFF2](https://www.w3.org/TR/WOFF2/)
- [EOT submission](https://www.w3.org/submissions/EOT/)
- [MTX submission](https://www.w3.org/submissions/MTX/)
- [Naming table](https://learn.microsoft.com/en-us/typography/opentype/spec/name)
- [OS/2 table](https://learn.microsoft.com/en-us/typography/opentype/spec/os2)
