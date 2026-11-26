using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Fonts.Metadata.Internal {
    internal static class FvarParser {
        const string TableTag = "fvar";
        const uint Version10 = 0x00010000;
        const int HeaderSize = 16, AxisSize = 20, TagSize = 4;
        const int VersionOffset = 0, ArrayOffsetField = 4, ReservedOffset = 6, AxisCountOffset = 8, AxisSizeOffset = 10;
        const int MinimumOffset = 4, DefaultOffset = 8, MaximumOffset = 12, FlagsOffset = 16, NameIdOffset = 18;
        const int ReservedValue = 2, FixedOne = 1 << 16;
        const ushort HiddenAxis = 0x0001, MinAxisNameId = 256, MaxAxisNameId = 32767;
        const string TruncatedHeader = "The fvar header is truncated.";
        const string UnsupportedVersion = "The fvar version is not supported.";
        const string InvalidArrayOffset = "The fvar axis array starts inside the header or outside the table.";
        const string InvalidAxisSize = "The fvar axis record size is invalid for version 1.0.";
        const string InvalidReservedValue = "The fvar reserved header field is not set to 2.";
        const string TruncatedArray = "The fvar axis array extends beyond the available table data.";
        const string AxisLimit = "MaxTotalVariationAxes prevented reading all fvar axes.";
        const string InvalidTag = "An fvar axis tag is not a valid design-variation tag.";
        const string InvalidRange = "An fvar axis does not satisfy minimum <= default <= maximum.";
        const string DuplicateTag = "The fvar axis tag duplicates an earlier readable axis.";
        const string ReservedFlags = "An fvar axis has reserved flag bits set.";
        const string InvalidNameId = "An fvar axis name ID is outside the range 256 through 32767.";
        internal static FontVariationAxes Read(ReadOnlySpan<byte> table, SourceLocation origin, ReadingContext context) {
            context.CheckCancellation();
            var diagnostics = context.NewSectionDiagnostics();
            FontVariationAxes InvalidHeader(string message, int offset) {
                Error(diagnostics, origin, message, offset);
                var retained = context.RetainSectionDiagnostics(diagnostics);
                return new FontVariationAxes(FontSectionStatus.Invalid, hasTableEntry: true, diagnostics: retained);
            }
            // The fvar 1.0 axis array has an explicit table-relative start and a fixed 20-byte stride.
            // Instance header fields and records are outside the axis reading contract.
            // https://learn.microsoft.com/en-us/typography/opentype/spec/fvar#fvar-header
            if(table.Length < HeaderSize)
                return InvalidHeader(TruncatedHeader, table.Length);
            if(MetadataTables.U32(table, VersionOffset) != Version10) {
                AddDiagnostic(diagnostics, origin, FontDiagnosticCodes.UnsupportedTableVersion, UnsupportedVersion,
                    VersionOffset, FontDiagnosticSeverity.Error, FontDiagnosticCategory.UnsupportedFeature);
                var retained = context.RetainSectionDiagnostics(diagnostics);
                return new FontVariationAxes(FontSectionStatus.Unsupported, hasTableEntry: true, diagnostics: retained);
            }
            int arrayOffset = MetadataTables.U16(table, ArrayOffsetField);
            if(arrayOffset < HeaderSize || arrayOffset > table.Length)
                return InvalidHeader(InvalidArrayOffset, ArrayOffsetField);
            if(MetadataTables.U16(table, AxisSizeOffset) != AxisSize)
                return InvalidHeader(InvalidAxisSize, AxisSizeOffset);
            if(MetadataTables.U16(table, ReservedOffset) != ReservedValue)
                Warning(diagnostics, origin, InvalidReservedValue, ReservedOffset);
            int declaredCount = MetadataTables.U16(table, AxisCountOffset);
            int availableCount = Math.Min(declaredCount, (table.Length - arrayOffset) / AxisSize);
            var axes = new List<FontVariationAxis>();
            var tags = new HashSet<uint>();
            int index = 0;
            for(; index < availableCount; index++) {
                context.CheckCancellation();
                if(!context.TryConsumeVariationAxis())
                    break;
                int recordOffset = arrayOffset + index * AxisSize;
                var record = table.Slice(recordOffset, AxisSize);
                var axis = ReadAxis(record, index, recordOffset, tags, diagnostics, origin);
                if(axis != null)
                    axes.Add(axis);
            }
            if(availableCount < declaredCount)
                Error(diagnostics, origin, TruncatedArray, arrayOffset + availableCount * AxisSize);
            bool malformed = diagnostics.HasErrors;
            bool limited = index < availableCount;
            if(limited) {
                AddDiagnostic(diagnostics, origin, FontDiagnosticCodes.LimitExceeded, AxisLimit,
                    arrayOffset + index * AxisSize, FontDiagnosticSeverity.Error, FontDiagnosticCategory.LimitExceeded, nameof(FontReadLimits.MaxTotalVariationAxes));
            }
            var status = GetStatus(malformed, limited, axes.Count);
            var snapshot = ResultList<FontVariationAxis>.Snapshot(axes);
            var retainedDiagnostics = context.RetainSectionDiagnostics(diagnostics);
            return new FontVariationAxes(status, snapshot, retainedDiagnostics);
        }
        static FontVariationAxis? ReadAxis(ReadOnlySpan<byte> record, int index, int offset, HashSet<uint> tags,
            DiagnosticBag diagnostics, SourceLocation origin) {
            var tagBytes = record[..TagSize];
            if(!IsAxisTag(tagBytes)) {
                Error(diagnostics, origin, InvalidTag, offset);
                return null;
            }
            // Fixed values are big-endian signed integers scaled by 2^-16. Compare the raw values before scaling.
            // https://learn.microsoft.com/en-us/typography/opentype/spec/otff#data-types
            int minimum = BinaryPrimitives.ReadInt32BigEndian(record[MinimumOffset..]);
            int defaultValue = BinaryPrimitives.ReadInt32BigEndian(record[DefaultOffset..]);
            int maximum = BinaryPrimitives.ReadInt32BigEndian(record[MaximumOffset..]);
            if(minimum > defaultValue || defaultValue > maximum) {
                Error(diagnostics, origin, InvalidRange, offset + DefaultOffset);
                return null;
            }
            uint tag = BinaryPrimitives.ReadUInt32BigEndian(tagBytes);
            if(!tags.Add(tag)) {
                Error(diagnostics, origin, DuplicateTag, offset);
                return null;
            }
            ushort flags = MetadataTables.U16(record, FlagsOffset);
            if((flags & ~HiddenAxis) != 0)
                Warning(diagnostics, origin, ReservedFlags, offset + FlagsOffset);
            ushort nameId = MetadataTables.U16(record, NameIdOffset);
            if(nameId is < MinAxisNameId or > MaxAxisNameId)
                Warning(diagnostics, origin, InvalidNameId, offset + NameIdOffset);
            return new FontVariationAxis(index, ContainerLayout.TagText(tag), (decimal)minimum / FixedOne,
                (decimal)defaultValue / FixedOne, (decimal)maximum / FixedOne, flags, nameId);
        }
        static bool IsAxisTag(ReadOnlySpan<byte> tag) {
            // Check the common syntax for registered and foundry tags without inferring registry membership.
            // https://learn.microsoft.com/en-us/typography/opentype/spec/dvaraxisreg#syntactic-requirements-for-design-variation-axis-tags
            if(!char.IsAsciiLetter((char)tag[0]))
                return false;
            foreach(byte character in tag[1..].TrimEnd((byte)' ')) {
                if(!char.IsAsciiLetterOrDigit((char)character))
                    return false;
            }
            return true;
        }
        static FontSectionStatus GetStatus(bool malformed, bool limited, int count) {
            if(!malformed && !limited)
                return FontSectionStatus.Complete;
            if(count > 0)
                return FontSectionStatus.Partial;
            return malformed ? FontSectionStatus.Invalid : FontSectionStatus.NotRead;
        }
        static void Error(DiagnosticBag diagnostics, SourceLocation origin, string message, int offset) {
            AddDiagnostic(diagnostics, origin, FontDiagnosticCodes.InvalidFvarTable, message,
                offset, FontDiagnosticSeverity.Error, FontDiagnosticCategory.MalformedData);
        }
        static void Warning(DiagnosticBag diagnostics, SourceLocation origin, string message, int offset) {
            AddDiagnostic(diagnostics, origin, FontDiagnosticCodes.NonConformantFvarTable, message,
                offset, FontDiagnosticSeverity.Warning, FontDiagnosticCategory.NonConformantData);
        }
        static void AddDiagnostic(DiagnosticBag diagnostics, SourceLocation origin, string code, string message, int offset,
            FontDiagnosticSeverity severity, FontDiagnosticCategory category, string? limitReason = null) {
            var diagnostic = new FontDiagnostic(code, severity, category, message,
                tableTag: TableTag, byteOffset: origin.Map(offset), tableByteOffset: offset, limitReason: limitReason);
            diagnostics.Add(diagnostic);
        }
    }
}
