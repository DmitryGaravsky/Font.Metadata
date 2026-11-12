using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Fonts.Metadata.Tool {
    internal sealed class PlainReportWriter : ReportWriter {
        const string LicenseNotice = "License hints are evidence, not a license decision; empty hints do not prove absence of a license.";
        readonly StreamWriter writer;
        bool first = true;

        internal PlainReportWriter(Stream output, CommandOptions options, CancellationToken token) : base(options, token) {
            writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        }
        internal static string Escape(string? value) {
            return JsonSerializer.Serialize(value);
        }
        internal override void Begin() {
            if(Options.AnalyzeLicenses)
                writer.WriteLine(LicenseNotice);
        }
        internal override void Write(SourceResult result) {
            Token.ThrowIfCancellationRequested();
            if(!first)
                writer.WriteLine();
            first = false;
            writer.WriteLine($"Source: {result.Source.Kind} {Escape(result.Source.Path)}");
            writer.WriteLine($"Status: {(result.Error == null ? ReportValues.Status(result.Inspection!.Status) : "ioError")}");
            if(result.Inspection?.Metadata is { } metadata)
                WriteMetadata(metadata);
            else
                writer.WriteLine("Metadata: null");
            if(result.Error != null) {
                writer.WriteLine($"Diagnostics:\n  error CLI_IO_ERROR (io): {Escape(result.Error)}");
            }
            else if(result.Inspection!.Diagnostics.Count == 0) {
                writer.WriteLine("Diagnostics: none");
            }
            else {
                writer.WriteLine("Diagnostics:");
                foreach(var diagnostic in result.Inspection.Diagnostics) {
                    Token.ThrowIfCancellationRequested();
                    writer.WriteLine($"  {ReportValues.Severity(diagnostic.Severity)} {diagnostic.Code} ({ReportValues.Category(diagnostic.Category)}): {Escape(diagnostic.Message)}");
                    writer.WriteLine($"    Face={Number(diagnostic.FaceIndex)}, table={Escape(diagnostic.TableTag)}, byteOffset={Number(diagnostic.ByteOffset)}, tableByteOffset={Number(diagnostic.TableByteOffset)}");
                }
            }
            writer.Flush();
        }
        void WriteMetadata(FontMetadata metadata) {
            writer.WriteLine($"Format: {ReportValues.Format(metadata.Format)}");
            writer.WriteLine($"EOT header status: {ReportValues.Section(metadata.EotHeaderStatus)}");
            if(metadata.Format == FontFormat.Eot) {
                if(metadata.Eot is not { } eot) {
                    writer.WriteLine("EOT header: null");
                }
                else {
                    writer.WriteLine("EOT header:");
                    writer.WriteLine($"  Format version: {Hex(eot.FormatVersion, 8)}");
                    writer.WriteLine($"  Flags: {Hex(eot.Flags, 8)}");
                    writer.WriteLine($"  Embedding permissions (raw): {Hex(eot.EmbeddingPermissions, 4)}");
                    writer.WriteLine($"  Family name: {Escape(eot.FamilyName)}");
                    writer.WriteLine($"  Style name: {Escape(eot.StyleName)}");
                    writer.WriteLine($"  Full name: {Escape(eot.FullName)}");
                    writer.WriteLine($"  Version: {Escape(eot.Version)}");
                    writer.WriteLine($"  EUDC code page: {Number(eot.EudcCodePage)}");
                    writer.WriteLine($"  EUDC flags: {Hex(eot.EudcFlags, 8)}");
                    writer.WriteLine($"  EUDC font data size: {Number(eot.EudcFontDataSize)}");
                }
            }
            foreach(var face in metadata.Faces) {
                Token.ThrowIfCancellationRequested();
                writer.WriteLine($"Face {Number(face.Index)}:");
                writer.WriteLine($"  Source: {ReportValues.Source(face.Source)}");
                writer.WriteLine($"  SFNT flavor: {Hex(face.SfntFlavor, 8)}");
                writer.WriteLine($"  Name table: {ReportValues.Section(face.NameTableStatus)}");
                writer.WriteLine($"  OS/2 table: {ReportValues.Section(face.Os2TableStatus)}");
                writer.WriteLine($"  Vendor ID: {Escape(face.VendorId)}");
                writer.WriteLine($"  Embedding permissions (raw): {Hex(face.EmbeddingPermissions, 4)}");
                foreach(var field in ReportValues.Preferred(face)) {
                    Token.ThrowIfCancellationRequested();
                    if(field.Value != null)
                        writer.WriteLine($"  {field.Label}: {Escape(field.Value)}");
                }
                if(Options.IncludeNameRecords) {
                    writer.WriteLine("  Name records:");
                    if(face.NameRecords.Count == 0)
                        writer.WriteLine("    none");
                    foreach(var record in face.NameRecords)
                        WriteRecord(record, "    ");
                }
                if(Options.AnalyzeLicenses) {
                    Token.ThrowIfCancellationRequested();
                    var analysis = FontLicenseAnalyzer.Analyze(face);
                    writer.WriteLine("  License analysis:");
                    writer.WriteLine($"    Source complete: {(analysis.IsSourceComplete ? "true" : "false")}");
                    if(analysis.Hints.Count == 0)
                        writer.WriteLine("    No recognized license hints");
                    foreach(var hint in analysis.Hints) {
                        Token.ThrowIfCancellationRequested();
                        writer.WriteLine($"    Hint: {ReportValues.IdentifierKind(hint.IdentifierKind)} {Escape(hint.Identifier)}, rule {hint.RuleId}");
                        WriteRecord(hint.SourceRecord, "      ");
                    }
                }
            }
        }
        void WriteRecord(FontMetadata.NameRecord record, string indent) {
            Token.ThrowIfCancellationRequested();
            writer.WriteLine($"{indent}Source record: index={Number(record.Index)}, nameId={Number(record.NameId)}, platformId={Number(record.PlatformId)}, encodingId={Number(record.EncodingId)}, languageId={Number(record.LanguageId)}, languageTag={Escape(record.LanguageTag)}");
            writer.WriteLine($"{indent}Value: {Escape(record.Value)}");
        }
        static string Number(long? value) {
            return value?.ToString(CultureInfo.InvariantCulture) ?? "null";
        }
        static string Hex(uint? value, int digits) {
            return value.HasValue ? "0x" + value.Value.ToString(digits == 4 ? "X4" : "X8", CultureInfo.InvariantCulture) : "null";
        }
        internal override void End() {
            Token.ThrowIfCancellationRequested();
            writer.Flush();
        }
        public override void Dispose() {
            writer.Dispose();
        }
    }
}
