using System;
using System.Collections.Generic;
using static Fonts.Metadata.Internal.MetadataTables;
using static Fonts.Metadata.Internal.NameTableDefinitions;

namespace Fonts.Metadata.Internal {
    internal static class CmapParser {
        internal static FontUnicodeCoverage Read(ReadOnlySpan<byte> table, SourceLocation origin, ReadingContext context) {
            var reader = new CmapReader(table, origin, context);
            return reader.Read();
        }
        // Encoding record offsets are relative to the cmap table, while lengths are local to each subtable.
        // https://learn.microsoft.com/en-us/typography/opentype/spec/cmap
        ref struct CmapReader {
            const string TableTag = "cmap";
            const int HeaderSize = 4, VersionOffset = 0, RecordCountOffset = 2, SupportedVersion = 0;
            const int RecordSize = 8, PlatformOffset = 0, EncodingOffset = 2, SubtableOffsetField = 4;
            const int FormatOffset = 0, Format4 = 4, Format12 = 12;
            const int UnicodeBmpEncoding = 3, UnicodeFullEncoding = 4;
            const int Format4LengthOffset = 2, Format4LanguageOffset = 4, SegmentCountBytesOffset = 6, EndCodesStart = 14;
            const int Format12HeaderSize = 16, Format12ReservedOffset = 2, Format12LengthOffset = 4;
            const int Format12LanguageOffset = 8, GroupCountOffset = 12, GroupSize = 12;
            const int GroupStartOffset = 0, GroupEndOffset = 4, GroupGlyphOffset = 8;
            const int FirstSurrogate = 0xD800, LastSurrogate = 0xDFFF, LastScalar = 0x10FFFF;
            const string TruncatedHeader = "The cmap header is truncated.";
            const string UnsupportedVersion = "The cmap version is not supported.";
            const string TruncatedDirectory = "The cmap encoding records extend outside the table.";
            const string InvalidSubtableOffset = "A Unicode encoding record points outside the subtable area.";
            const string UnsupportedSubtable = "The cmap has no supported Unicode format 4 or 12 subtable.";
            const string InvalidSubtableHeader = "The selected cmap subtable header is invalid or truncated.";
            const string InvalidSubtableLength = "The declared cmap subtable length is invalid or extends outside the table.";
            const string InvalidGroupCount = "The cmap format 12 group count does not match the declared subtable length.";
            const string InvalidGroup = "A cmap format 12 group is out of order, overlaps, or has invalid character codes or glyph IDs.";
            const string InvalidSegmentArrays = "The cmap format 4 segment arrays are invalid or truncated.";
            const string InvalidSegment = "A cmap format 4 segment is reversed, out of order, or overlapping.";
            const string InvalidTerminator = "The final cmap format 4 segment must start and end at U+FFFF.";
            const string InvalidGlyphOffset = "A cmap format 4 glyph range is unaligned or outside the glyph ID array.";
            const string WorkLimit = "MaxTotalCmapWork prevented reading all cmap mappings.";
            const string RangeLimit = "MaxTotalUnicodeRanges prevented retaining all Unicode coverage ranges.";
            readonly ReadOnlySpan<byte> table;
            readonly SourceLocation origin;
            readonly ReadingContext context;
            readonly DiagnosticBag diagnostics;
            readonly List<FontUnicodeRange> ranges;
            Candidate? selected;
            bool malformed, limited;
            internal CmapReader(ReadOnlySpan<byte> table, SourceLocation origin, ReadingContext context) {
                this.table = table;
                this.origin = origin;
                this.context = context;
                diagnostics = context.NewSectionDiagnostics();
                ranges = new();
            }
            internal FontUnicodeCoverage Read() {
                context.CheckCancellation();
                if(table.Length < HeaderSize) {
                    Invalid(TruncatedHeader, table.Length);
                    return Result();
                }
                if(U16(table, VersionOffset) != SupportedVersion) {
                    Error(FontDiagnosticCodes.UnsupportedTableVersion, UnsupportedVersion, VersionOffset, FontDiagnosticCategory.UnsupportedFeature);
                    return Result();
                }
                int recordCount = U16(table, RecordCountOffset);
                int directoryEnd = HeaderSize + recordCount * RecordSize;
                if(directoryEnd > table.Length) {
                    Invalid(TruncatedDirectory, RecordCountOffset);
                    return Result();
                }
                if(!Select(directoryEnd))
                    return Result();
                if(selected is not Candidate candidate) {
                    if(!malformed)
                        Error(FontDiagnosticCodes.UnsupportedCmapSubtable, UnsupportedSubtable, RecordCountOffset, FontDiagnosticCategory.UnsupportedFeature);
                    return Result();
                }
                var subtable = table[candidate.Offset..];
                if(candidate.Format == Format12)
                    ReadFormat12(subtable, candidate.Offset);
                else
                    ReadFormat4(subtable, candidate.Offset);
                return Result();
            }
            bool Select(int directoryEnd) {
                Candidate? best = null;
                int unreadablePriority = 0, unreadableOffsetField = 0;
                for(int record = HeaderSize; record < directoryEnd; record += RecordSize) {
                    if(!ConsumeWork(record))
                        return false;
                    ushort platform = U16(table, record + PlatformOffset);
                    ushort encoding = U16(table, record + EncodingOffset);
                    var (expectedFormat, priority) = GetPreference(platform, encoding);
                    if(priority == 0 || (best is Candidate current && priority <= current.Priority))
                        continue;
                    int offsetField = record + SubtableOffsetField;
                    uint offset = U32(table, offsetField);
                    if(offset < directoryEnd || offset > table.Length - sizeof(ushort)) {
                        if(priority > unreadablePriority) {
                            unreadablePriority = priority;
                            unreadableOffsetField = offsetField;
                        }
                        continue;
                    }
                    ushort format = U16(table, (int)offset + FormatOffset);
                    if(format == expectedFormat)
                        best = new Candidate((int)offset, priority, format, platform, encoding);
                }
                // An unreadable record only affects selection if it could outrank the chosen record.
                if(unreadablePriority != 0 && (best is null || unreadablePriority >= best.Value.Priority))
                    Invalid(InvalidSubtableOffset, unreadableOffsetField);
                selected = best;
                return true;
            }
            static (ushort Format, int Priority) GetPreference(ushort platform, ushort encoding) {
                // Prefer full repertoire, then Windows; for BMP Unicode encodings prefer newer semantics.
                // Preserve first record order if a platform and encoding are duplicated.
                return (platform, encoding) switch {
                    (WindowsPlatformId, WindowsUnicodeFullEncodingId) => (Format12, 7),
                    (UnicodePlatformId, UnicodeFullEncoding) => (Format12, 6),
                    (WindowsPlatformId, WindowsUnicodeBmpEncodingId) => (Format4, 5),
                    (UnicodePlatformId, <= UnicodeBmpEncoding) => (Format4, encoding + 1),
                    _ => default
                };
            }
            void ReadFormat12(ReadOnlySpan<byte> subtable, int offset) {
                if(subtable.Length < Format12HeaderSize || U16(subtable, Format12ReservedOffset) != 0 ||
                    U32(subtable, Format12LanguageOffset) != 0) {
                    Invalid(InvalidSubtableHeader, offset);
                    return;
                }
                uint length = U32(subtable, Format12LengthOffset);
                uint count = U32(subtable, GroupCountOffset);
                if(length != Format12HeaderSize + (long)count * GroupSize) {
                    Invalid(InvalidGroupCount, offset + GroupCountOffset);
                    return;
                }
                subtable = BoundSubtable(subtable, offset + Format12LengthOffset, length);
                // The sequential group assigns increasing 16-bit glyph IDs without format 4's wraparound.
                // https://learn.microsoft.com/en-us/typography/opentype/spec/cmap#format-12-segmented-coverage
                int previousEnd = -1;
                for(int group = Format12HeaderSize; group <= subtable.Length - GroupSize; group += GroupSize) {
                    if(!ConsumeWork(offset + group))
                        return;
                    uint start = U32(subtable, group + GroupStartOffset);
                    uint end = U32(subtable, group + GroupEndOffset);
                    uint firstGlyph = U32(subtable, group + GroupGlyphOffset);
                    if(start <= previousEnd || start > end || end > LastScalar || (long)firstGlyph + (end - start) > ushort.MaxValue) {
                        Invalid(InvalidGroup, offset + group);
                        return;
                    }
                    previousEnd = (int)end;
                    int firstMapped = (int)start + (firstGlyph == 0 ? 1 : 0);
                    if(!AppendScalarRanges(firstMapped, previousEnd, offset + group))
                        return;
                }
            }
            void ReadFormat4(ReadOnlySpan<byte> subtable, int offset) {
                if(subtable.Length < EndCodesStart || U16(subtable, Format4LanguageOffset) != 0) {
                    Invalid(InvalidSubtableHeader, offset);
                    return;
                }
                int length = U16(subtable, Format4LengthOffset);
                if(length < EndCodesStart || length % sizeof(ushort) != 0) {
                    Invalid(InvalidSubtableLength, offset + Format4LengthOffset);
                    return;
                }
                subtable = BoundSubtable(subtable, offset + Format4LengthOffset, (uint)length);
                int segmentBytes = U16(subtable, SegmentCountBytesOffset);
                int reservedPad = EndCodesStart + segmentBytes;
                int startCodes = reservedPad + sizeof(ushort);
                int deltas = startCodes + segmentBytes;
                int rangeOffsets = deltas + segmentBytes;
                int glyphArray = rangeOffsets + segmentBytes;
                if(segmentBytes == 0 || segmentBytes % sizeof(ushort) != 0 ||
                    glyphArray > subtable.Length || U16(subtable, reservedPad) != 0) {
                    Invalid(InvalidSegmentArrays, offset + SegmentCountBytesOffset);
                    return;
                }
                int previousEnd = -1;
                for(int slot = 0; slot < segmentBytes; slot += sizeof(ushort)) {
                    int location = offset + EndCodesStart + slot;
                    if(!ConsumeWork(location))
                        return;
                    int start = U16(subtable, startCodes + slot);
                    int end = U16(subtable, EndCodesStart + slot);
                    if(start > end || start <= previousEnd) {
                        Invalid(InvalidSegment, location);
                        return;
                    }
                    if(slot == segmentBytes - sizeof(ushort) && (start != ushort.MaxValue || end != ushort.MaxValue)) {
                        Invalid(InvalidTerminator, location);
                        return;
                    }
                    previousEnd = end;
                    int delta = U16(subtable, deltas + slot);
                    int rangeWord = rangeOffsets + slot;
                    int relative = U16(subtable, rangeWord);
                    if(relative == 0) {
                        int zero = -delta & ushort.MaxValue;
                        if(!AppendScalarRanges(start, Math.Min(end, zero - 1), location) ||
                            !AppendScalarRanges(Math.Max(start, zero + 1), end, location))
                            return;
                    }
                    else if(!ReadGlyphArray(subtable, offset, rangeWord, glyphArray, start, end, delta)) {
                        return;
                    }
                }
            }
            bool ReadGlyphArray(ReadOnlySpan<byte> subtable, int offset, int rangeWord, int glyphArray, int start, int end, int delta) {
                // idRangeOffset is measured from its own word, and must address words in glyphIdArray.
                // https://learn.microsoft.com/en-us/typography/opentype/spec/cmap#format-4-segment-mapping-to-delta-values
                int firstGlyph = rangeWord + U16(subtable, rangeWord);
                int glyphBytes = (end - start + 1) * sizeof(ushort);
                if(firstGlyph % sizeof(ushort) != 0 || firstGlyph < glyphArray || firstGlyph > subtable.Length - glyphBytes) {
                    Invalid(InvalidGlyphOffset, offset + rangeWord);
                    return false;
                }
                for(int code = start, glyph = firstGlyph; code <= end; code++, glyph += sizeof(ushort)) {
                    if(!ConsumeWork(offset + glyph))
                        return false;
                    int raw = U16(subtable, glyph);
                    if(raw != 0 && ((raw + delta) & ushort.MaxValue) != 0 && (code < FirstSurrogate || code > LastSurrogate)) {
                        if(!AppendRange(code, code, offset + glyph))
                            return false;
                    }
                }
                return true;
            }
            ReadOnlySpan<byte> BoundSubtable(ReadOnlySpan<byte> subtable, int lengthOffset, uint length) {
                if(length <= subtable.Length)
                    return subtable[..(int)length];
                Invalid(InvalidSubtableLength, lengthOffset);
                return subtable;
            }
            bool AppendScalarRanges(int start, int end, int offset) {
                return AppendRange(start, Math.Min(end, FirstSurrogate - 1), offset) &&
                    AppendRange(Math.Max(start, LastSurrogate + 1), end, offset);
            }
            bool AppendRange(int start, int end, int offset) {
                if(start > end)
                    return true;
                if(ranges.Count != 0 && ranges[^1].End + 1 == start) {
                    ranges[^1] = new FontUnicodeRange(ranges[^1].Start, end);
                    return true;
                }
                if(!context.TryRetainUnicodeRange()) {
                    Error(FontDiagnosticCodes.LimitExceeded, RangeLimit, offset, FontDiagnosticCategory.LimitExceeded);
                    return false;
                }
                ranges.Add(new FontUnicodeRange(start, end));
                return true;
            }
            bool ConsumeWork(int offset) {
                if(context.TryConsumeCmapWork())
                    return true;
                Error(FontDiagnosticCodes.LimitExceeded, WorkLimit, offset, FontDiagnosticCategory.LimitExceeded);
                return false;
            }
            void Invalid(string message, int offset) {
                Error(FontDiagnosticCodes.InvalidCmapTable, message, offset, FontDiagnosticCategory.MalformedData);
            }
            void Error(string code, string message, int offset, FontDiagnosticCategory category) {
                malformed |= category == FontDiagnosticCategory.MalformedData;
                limited |= category == FontDiagnosticCategory.LimitExceeded;
                var diagnostic = new FontDiagnostic(code, FontDiagnosticSeverity.Error, category, message,
                    tableTag: TableTag, byteOffset: origin.Map(offset), tableByteOffset: offset);
                diagnostics.Add(diagnostic);
            }
            FontUnicodeCoverage Result() {
                var status = GetStatus();
                var snapshot = ResultList<FontUnicodeRange>.Snapshot(ranges);
                var retainedDiagnostics = context.RetainSectionDiagnostics(diagnostics);
                return new FontUnicodeCoverage(status, selected?.Format, selected?.Platform, selected?.Encoding, snapshot, retainedDiagnostics);
            }
            FontSectionStatus GetStatus() {
                if(!diagnostics.HasErrors)
                    return FontSectionStatus.Complete;
                if(ranges.Count != 0)
                    return FontSectionStatus.Partial;
                if(malformed)
                    return FontSectionStatus.Invalid;
                return limited ? FontSectionStatus.NotRead : FontSectionStatus.Unsupported;
            }
        }
        readonly record struct Candidate(int Offset, int Priority, ushort Format, ushort Platform, ushort Encoding);
    }
}
