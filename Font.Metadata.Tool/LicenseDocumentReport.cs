using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Spdx.Expressions;

namespace Fonts.Metadata.Tool {
    internal sealed class LicenseDocumentReport : IDisposable {
        readonly Stream output;
        readonly FontLicenseDocumentOptions options;
        readonly SpdxRegistryIdentity registry;
        readonly CancellationToken token;
        readonly Utf8JsonWriter? json;
        readonly StreamWriter? plain;
        internal LicenseDocumentReport(Stream output, bool useJson, FontLicenseDocumentOptions options,
            SpdxRegistryIdentity registry, CancellationToken token) {
            this.output = output;
            this.options = options;
            this.registry = registry;
            this.token = token;
            if(useJson)
                json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
            else
                plain = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        }
        internal void Begin() {
            if(json is not { } writer) {
                plain!.WriteLine($"External license documents (SPDX catalogue {PlainReportWriter.Escape(registry.LicenseListVersion)})");
                return;
            }
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteStartObject("options");
            writer.WriteNumber("maxInputBytes", LicenseDocumentCommand.MaxInputBytes);
            writer.WriteNumber("maxSourceCharacters", options.MaxSourceCharacters);
            writer.WriteNumber("maxEvidence", options.MaxEvidence);
            writer.WriteEndObject();
            writer.WriteStartObject("spdxContext");
            writer.WriteString("recognitionProfile", "license-document-v1");
            writer.WriteString("syntaxProfile", "SPDX-2.3.1");
            SpdxJson.WriteRegistry(writer, registry);
            writer.WriteEndObject();
            writer.WriteStartArray("results");
            writer.Flush();
        }
        internal void Write(LicenseDocumentResult result) {
            token.ThrowIfCancellationRequested();
            if(json is { } writer)
                WriteJson(writer, result);
            else
                WritePlain(plain!, result);
        }
        void WriteJson(Utf8JsonWriter writer, LicenseDocumentResult result) {
            writer.WriteStartObject();
            writer.WriteStartObject("source");
            writer.WriteString("kind", "file");
            writer.WriteString("path", result.Path);
            writer.WriteEndObject();
            writer.WriteString("status", Status(result.Status));
            writer.WriteString("sha256", result.Sha256);
            if(result.HasUtf8Bom is bool hasBom)
                writer.WriteBoolean("hasUtf8Bom", hasBom);
            else
                writer.WriteNull("hasUtf8Bom");
            writer.WriteStartArray("evidence");
            foreach(var item in result.Analysis?.Evidence ?? ReadOnlyCollection<FontLicenseDocumentEvidence>.Empty) {
                Checkpoint();
                WriteEvidence(writer, item);
            }
            writer.WriteEndArray();
            writer.WriteStartArray("diagnostics");
            if(result.Code is { } code)
                WriteDiagnostic(writer, code, result.Message!, null, null);
            if(result.Analysis?.Diagnostic is { } diagnostic)
                WriteDiagnostic(writer, diagnostic.Code, diagnostic.Message, diagnostic.SourceStart, diagnostic.SourceLength);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }
        void WriteEvidence(Utf8JsonWriter writer, FontLicenseDocumentEvidence item) {
            writer.WriteStartObject();
            writer.WriteString("origin", Origin(item.Origin));
            writer.WriteString("identifier", item.Identifier);
            writer.WriteNumber("sourceStart", item.SourceStart);
            writer.WriteNumber("sourceLength", item.SourceLength);
            writer.WriteString("sourceText", item.SourceText);
            WriteNumber(writer, "expressionStart", item.ExpressionStart);
            WriteNumber(writer, "expressionLength", item.ExpressionLength);
            writer.WriteString("expressionText", item.ExpressionText);
            writer.WriteString("parseStatus", item.ParseResult is { } parsed ? SpdxReportValues.Status(parsed.Status) : null);
            SpdxJson.WriteDiagnostics(writer, "parseDiagnostics", item.ParseResult?.Diagnostics ?? ReadOnlyCollection<SpdxDiagnostic>.Empty, Checkpoint);
            writer.WritePropertyName("validation");
            if(item.ValidationResult is { } validation)
                SpdxJson.WriteValidation(writer, validation, Checkpoint);
            else
                writer.WriteNullValue();
            writer.WriteEndObject();
        }
        static void WriteDiagnostic(Utf8JsonWriter writer, string code, string message, int? start, int? length) {
            writer.WriteStartObject();
            writer.WriteString("code", code);
            writer.WriteString("message", message);
            WriteNumber(writer, "sourceStart", start);
            WriteNumber(writer, "sourceLength", length);
            writer.WriteEndObject();
        }
        static void WriteNumber(Utf8JsonWriter writer, string property, int? value) {
            if(value is int number)
                writer.WriteNumber(property, number);
            else
                writer.WriteNull(property);
        }
        void WritePlain(TextWriter writer, LicenseDocumentResult result) {
            writer.WriteLine();
            writer.WriteLine($"Source: {PlainReportWriter.Escape(result.Path)}");
            writer.WriteLine($"Status: {Status(result.Status)}");
            if(result.Sha256 is { } sha256)
                writer.WriteLine($"SHA-256: {sha256}");
            if(result.Analysis is { } analysis) {
                foreach(var item in analysis.Evidence) {
                    token.ThrowIfCancellationRequested();
                    string range = string.Create(CultureInfo.InvariantCulture, $"UTF-16 offset {item.SourceStart}, length {item.SourceLength}");
                    writer.WriteLine($"  {Origin(item.Origin)} ({range}): {Describe(item)}");
                    if(item.ParseResult is { } parsed)
                        WritePlainDiagnostics(writer, parsed.Diagnostics);
                    if(item.ValidationResult is { } validation)
                        WritePlainDiagnostics(writer, validation.Diagnostics);
                }
                if(analysis.Evidence.Count == 0 && analysis.IsAnalysisComplete)
                    writer.WriteLine("No recognized evidence.");
                if(analysis.Diagnostic is { } diagnostic)
                    writer.WriteLine($"  {diagnostic.Code}: {PlainReportWriter.Escape(diagnostic.Message)}");
            }
            if(result.Code is { } code)
                writer.WriteLine($"  {code}: {PlainReportWriter.Escape(result.Message)}");
            writer.Flush();
        }
        void WritePlainDiagnostics(TextWriter writer, IReadOnlyList<SpdxDiagnostic> diagnostics) {
            foreach(var diagnostic in diagnostics) {
                token.ThrowIfCancellationRequested();
                writer.WriteLine($"    {SpdxReportValues.Severity(diagnostic.Severity)} {diagnostic.Code}: {PlainReportWriter.Escape(diagnostic.Message)}");
            }
        }
        static string Describe(FontLicenseDocumentEvidence item) {
            if(item.ParseResult is { } parsed)
                return $"{PlainReportWriter.Escape(item.ExpressionText)} ({SpdxReportValues.Status(parsed.Status)})";
            if(item.Origin == FontLicenseDocumentOrigin.Header)
                return PlainReportWriter.Escape(item.SourceText);
            return item.Identifier!;
        }
        void Checkpoint() {
            token.ThrowIfCancellationRequested();
            if(json!.BytesPending >= 16384)
                json.Flush();
        }
        internal void End() {
            token.ThrowIfCancellationRequested();
            if(json is { } writer) {
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.Flush();
                output.WriteByte((byte)'\n');
            }
            output.Flush();
        }
        public void Dispose() {
            json?.Dispose();
            plain?.Dispose();
        }
        static string Origin(FontLicenseDocumentOrigin origin) {
            return origin switch {
                FontLicenseDocumentOrigin.SpdxDeclaration => "spdxDeclaration",
                FontLicenseDocumentOrigin.Header => "header",
                FontLicenseDocumentOrigin.FullText => "fullText",
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
        }
        static string Status(LicenseDocumentStatus status) {
            return status switch {
                LicenseDocumentStatus.Complete => "complete",
                LicenseDocumentStatus.LimitExceeded => "limitExceeded",
                LicenseDocumentStatus.InvalidEncoding => "invalidEncoding",
                LicenseDocumentStatus.IoError => "ioError",
                _ => throw new ArgumentOutOfRangeException(nameof(status))
            };
        }
    }
}
