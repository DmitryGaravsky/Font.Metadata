using System.IO;
using System.Text.Json;
using System.Threading;
using Spdx.Expressions;

namespace Fonts.Metadata.Tool {
    internal sealed partial class JsonReportWriter : ReportWriter {
        readonly SpdxLicenseRegistry? registry;
        readonly FontSpdxOptions spdxOptions = new();
        readonly Utf8JsonWriter writer;
        readonly Stream output;
        const int FlushThreshold = 16384;

        internal JsonReportWriter(Stream output, CommandOptions options, CancellationToken token, SpdxLicenseRegistry? registry = null) : base(options, token) {
            this.output = output;
            this.registry = registry;
            writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
        }
        internal override void Begin() {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteStartObject("options");
            writer.WriteBoolean("includeNameRecords", Options.IncludeNameRecords);
            writer.WriteBoolean("analyzeLicenses", Options.AnalyzeLicenses);
            writer.WriteNumber("maxInputBytes", Options.MaxInputBytes);
            if(Options.AnalyzeSpdx)
                writer.WriteBoolean("analyzeSpdx", true);
            writer.WriteEndObject();
            if(Options.AnalyzeSpdx)
                WriteSpdxContext();
            writer.WriteStartArray("results");
            writer.Flush();
        }
        internal override void Write(SourceResult result) {
            Token.ThrowIfCancellationRequested();
            writer.WriteStartObject();
            writer.WriteStartObject("source");
            writer.WriteString("kind", result.Source.Kind);
            writer.WriteString("path", result.Source.Path);
            writer.WriteEndObject();
            writer.WriteString("status", result.Error == null ? ReportValues.Status(result.Inspection!.Status) : "ioError");
            writer.WritePropertyName("metadata");
            if(result.Inspection?.Metadata is { } metadata)
                WriteMetadata(metadata);
            else
                writer.WriteNullValue();
            writer.WriteStartArray("diagnostics");
            if(result.Error != null) {
                WriteDiagnostic("CLI_IO_ERROR", "error", "io", result.Error, null, null, null, null);
            }
            else {
                foreach(var diagnostic in result.Inspection!.Diagnostics) {
                    WriteDiagnostic(diagnostic.Code, ReportValues.Severity(diagnostic.Severity),
                        ReportValues.Category(diagnostic.Category), diagnostic.Message, diagnostic.FaceIndex,
                        diagnostic.TableTag, diagnostic.ByteOffset, diagnostic.TableByteOffset);
                }
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }
        void WriteMetadata(FontMetadata metadata) {
            writer.WriteStartObject();
            writer.WriteString("format", ReportValues.Format(metadata.Format));
            writer.WriteString("eotHeaderStatus", ReportValues.Section(metadata.EotHeaderStatus));
            writer.WritePropertyName("eot");
            if(metadata.Eot is { } eot) {
                writer.WriteStartObject();
                Number("formatVersion", eot.FormatVersion);
                Number("flags", eot.Flags);
                Number("embeddingPermissions", eot.EmbeddingPermissions);
                writer.WriteString("familyName", eot.FamilyName);
                writer.WriteString("styleName", eot.StyleName);
                writer.WriteString("fullName", eot.FullName);
                writer.WriteString("version", eot.Version);
                Number("eudcCodePage", eot.EudcCodePage);
                Number("eudcFlags", eot.EudcFlags);
                Number("eudcFontDataSize", eot.EudcFontDataSize);
                writer.WriteEndObject();
            }
            else
                writer.WriteNullValue();
            writer.WriteStartArray("faces");
            foreach(var face in metadata.Faces) {
                Checkpoint();
                writer.WriteStartObject();
                writer.WriteNumber("index", face.Index);
                writer.WriteString("source", ReportValues.Source(face.Source));
                Number("sfntFlavor", face.SfntFlavor);
                writer.WriteString("nameTableStatus", ReportValues.Section(face.NameTableStatus));
                writer.WriteString("os2TableStatus", ReportValues.Section(face.Os2TableStatus));
                writer.WriteString("vendorId", face.VendorId);
                Number("embeddingPermissions", face.EmbeddingPermissions);
                foreach(var field in ReportValues.Preferred(face)) {
                    Checkpoint();
                    writer.WriteString(field.Key, field.Value);
                }
                if(Options.IncludeNameRecords) {
                    writer.WriteStartArray("nameRecords");
                    foreach(var record in face.NameRecords)
                        WriteRecord(record);
                    writer.WriteEndArray();
                }
                if(Options.AnalyzeLicenses) {
                    Token.ThrowIfCancellationRequested();
                    var analysis = FontLicenseAnalyzer.Analyze(face);
                    writer.WriteStartObject("licenseAnalysis");
                    writer.WriteBoolean("isSourceComplete", analysis.IsSourceComplete);
                    writer.WriteStartArray("hints");
                    foreach(var hint in analysis.Hints) {
                        Checkpoint();
                        writer.WriteStartObject();
                        writer.WriteString("identifier", hint.Identifier);
                        writer.WriteString("identifierKind", ReportValues.IdentifierKind(hint.IdentifierKind));
                        writer.WriteString("ruleId", hint.RuleId);
                        writer.WritePropertyName("sourceRecord");
                        WriteRecord(hint.SourceRecord);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                if(Options.AnalyzeSpdx)
                    WriteSpdx(FontSpdxAnalyzer.Analyze(face, registry!, spdxOptions, Token));
                writer.WriteEndObject();
                Checkpoint();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        void WriteRecord(FontMetadata.NameRecord record) {
            Checkpoint();
            writer.WriteStartObject();
            writer.WriteNumber("index", record.Index);
            writer.WriteNumber("nameId", record.NameId);
            writer.WriteNumber("platformId", record.PlatformId);
            writer.WriteNumber("encodingId", record.EncodingId);
            writer.WriteNumber("languageId", record.LanguageId);
            writer.WriteString("languageTag", record.LanguageTag);
            writer.WriteString("value", record.Value);
            writer.WriteEndObject();
            Checkpoint();
        }
        void WriteDiagnostic(string code, string severity, string category, string message,
            int? face, string? table, long? offset, long? tableOffset) {
            Checkpoint();
            writer.WriteStartObject();
            writer.WriteString("code", code);
            writer.WriteString("severity", severity);
            writer.WriteString("category", category);
            writer.WriteString("message", message);
            Number("faceIndex", face);
            writer.WriteString("tableTag", table);
            Number("byteOffset", offset);
            Number("tableByteOffset", tableOffset);
            writer.WriteEndObject();
        }
        void Number(string name, long? value) {
            if(value.HasValue)
                writer.WriteNumber(name, value.Value);
            else
                writer.WriteNull(name);
        }
        void Checkpoint() {
            Token.ThrowIfCancellationRequested();
            if(writer.BytesPending >= FlushThreshold)
                writer.Flush();
        }
        internal override void End() {
            Token.ThrowIfCancellationRequested();
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
            output.WriteByte((byte)'\n');
            output.Flush();
        }
        public override void Dispose() {
            writer.Dispose();
        }
    }
}
