using System;
using System.Buffers.Binary;

namespace Fonts.Metadata.Internal {
    internal static class HeaderTables {
        const string Truncated = "The table is shorter than required for its version.";
        const string Version = "This table version is unsupported.";
        const string Magic = "The head magic number is invalid.";
        const string EmRange = "The head unitsPerEm field is outside the range 16 through 16384.";
        internal static TableMetadata Read(uint tag, ReadOnlySpan<byte> data, SourceLocation source) {
            bool head = tag == ContainerLayout.HeadTag;
            string name = head ? "head" : "maxp";
            string invalidCode = head ? FontDiagnosticCodes.InvalidHeadTable : FontDiagnosticCodes.InvalidMaxpTable;
            FontDiagnostic Diagnostic(string code, string message, int offset, FontDiagnosticCategory category,
                FontDiagnosticSeverity severity = FontDiagnosticSeverity.Error) {
                return new FontDiagnostic(code, severity, category, message, tableTag: name,
                    byteOffset: source.Map(offset), tableByteOffset: offset);
            }
            TableMetadata Failure(string code, string message, int offset, bool unsupported = false) {
                return new TableMetadata {
                    Status = unsupported ? FontSectionStatus.Unsupported : FontSectionStatus.Invalid,
                    Diagnostics = [Diagnostic(code, message, offset,
                        unsupported ? FontDiagnosticCategory.UnsupportedFeature : FontDiagnosticCategory.MalformedData)]
                };
            }
            if(data.Length < sizeof(uint) || (head && data.Length < 54))
                return Failure(invalidCode, Truncated, data.Length);
            uint version = MetadataTables.U32(data, 0);
            // OpenType maxp: version 0.5 has six bytes, version 1 has 32; numGlyphs is at +4.
            // https://learn.microsoft.com/typography/opentype/spec/maxp
            if(version != 0x00010000 && (head || version != 0x00005000))
                return Failure(FontDiagnosticCodes.UnsupportedTableVersion, Version, 0, true);
            int required = head ? 54 : version == 0x00005000 ? 6 : 32;
            if(data.Length < required)
                return Failure(invalidCode, Truncated, data.Length);
            if(!head)
                return new TableMetadata { Status = FontSectionStatus.Complete, GlyphCount = MetadataTables.U16(data, 4) };
            // OpenType head: revision +4, magic +12, flags/em +16/+18, LONGDATETIME +20/+28, macStyle +44.
            // https://learn.microsoft.com/typography/opentype/spec/head
            if(MetadataTables.U32(data, 12) != 0x5F0F3CF5)
                return Failure(invalidCode, Magic, 12);
            ushort em = MetadataTables.U16(data, 18);
            return new TableMetadata {
                Status = FontSectionStatus.Complete,
                Head = new FontHeader(BinaryPrimitives.ReadInt32BigEndian(data.Slice(4)),
                    BinaryPrimitives.ReadInt64BigEndian(data.Slice(20)), BinaryPrimitives.ReadInt64BigEndian(data.Slice(28)),
                    em, MetadataTables.U16(data, 16), MetadataTables.U16(data, 44)),
                Diagnostics = em is < 16 or > 16384
                    ? [Diagnostic(FontDiagnosticCodes.NonConformantHead, EmRange, 18, FontDiagnosticCategory.NonConformantData, FontDiagnosticSeverity.Warning)]
                    : []
            };
        }
    }
}
