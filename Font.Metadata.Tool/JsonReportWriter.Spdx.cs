using System;
using System.Collections.Generic;
using System.Text.Json;
using Spdx.Expressions;

namespace Fonts.Metadata.Tool {
    internal sealed partial class JsonReportWriter {
        void WriteSpdxContext() {
            var identity = registry!.Identity;
            writer.WriteStartObject("spdxContext");
            writer.WriteString("recognitionProfile", "font-name13-v1");
            writer.WriteString("syntaxProfile", "SPDX-2.3.1");
            SpdxJson.WriteRegistry(writer, identity);
            writer.WriteStartObject("options");
            writer.WriteNumber("maxSourceRecords", spdxOptions.MaxSourceRecords);
            writer.WriteNumber("maxTotalSourceCharacters", spdxOptions.MaxTotalSourceCharacters);
            writer.WriteNumber("maxDeclarations", spdxOptions.MaxDeclarations);
            writer.WriteNumber("maxDiagnostics", spdxOptions.MaxDiagnostics);
            writer.WriteStartObject("parseOptions");
            writer.WriteNumber("maxInputLength", spdxOptions.ParseOptions.MaxInputLength);
            writer.WriteNumber("maxIdentifierLength", spdxOptions.ParseOptions.MaxIdentifierLength);
            writer.WriteNumber("maxTokens", spdxOptions.ParseOptions.MaxTokens);
            writer.WriteNumber("maxNodes", spdxOptions.ParseOptions.MaxNodes);
            writer.WriteNumber("maxParenthesisDepth", spdxOptions.ParseOptions.MaxParenthesisDepth);
            writer.WriteEndObject();
            writer.WriteStartObject("validationOptions");
            writer.WriteNumber("maxDiagnostics", spdxOptions.ValidationOptions.MaxDiagnostics);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        void WriteSpdx(FontSpdxAnalysis analysis) {
            writer.WriteStartObject("spdxAnalysis");
            writer.WriteBoolean("isSourceComplete", analysis.IsSourceComplete);
            writer.WriteBoolean("isAnalysisComplete", analysis.IsAnalysisComplete);
            writer.WriteBoolean("diagnosticsTruncated", analysis.DiagnosticsTruncated);
            writer.WriteStartArray("declarations");
            foreach(var declaration in analysis.Declarations) {
                Checkpoint();
                writer.WriteStartObject();
                writer.WriteString("ruleId", declaration.RuleId);
                writer.WritePropertyName("sourceRecord");
                WriteRecord(declaration.SourceRecord);
                writer.WriteNumber("sourceStart", declaration.SourceStart);
                writer.WriteNumber("sourceLength", declaration.SourceLength);
                writer.WriteString("expressionText", declaration.ExpressionText);
                writer.WriteString("parseStatus", SpdxReportValues.Status(declaration.ParseResult.Status));
                SpdxJson.WriteDiagnostics(writer, "parseDiagnostics", declaration.ParseResult.Diagnostics, Checkpoint);
                writer.WritePropertyName("validation");
                if(declaration.ValidationResult is { } validation)
                    SpdxJson.WriteValidation(writer, validation, Checkpoint);
                else
                    writer.WriteNullValue();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("diagnostics");
            foreach(var diagnostic in analysis.Diagnostics) {
                Checkpoint();
                writer.WriteStartObject();
                writer.WriteString("code", diagnostic.Code);
                writer.WriteString("message", diagnostic.Message);
                writer.WritePropertyName("sourceRecord");
                WriteRecord(diagnostic.SourceRecord);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }
    internal static class SpdxJson {
        internal static void WriteRegistry(Utf8JsonWriter writer, SpdxRegistryIdentity identity) {
            writer.WriteStartObject("registry");
            writer.WriteString("licenseListVersion", identity.LicenseListVersion);
            writer.WriteString("licensesSha256", identity.LicensesSha256);
            writer.WriteString("exceptionsSha256", identity.ExceptionsSha256);
            writer.WriteEndObject();
        }
        internal static void WriteValidation(Utf8JsonWriter writer, SpdxValidationResult validation, Action checkpoint) {
            writer.WriteStartObject();
            writer.WriteBoolean("hasIdentifierErrors", validation.HasIdentifierErrors);
            writer.WriteBoolean("hasUnresolvedReferences", validation.HasUnresolvedReferences);
            writer.WriteBoolean("diagnosticsTruncated", validation.DiagnosticsTruncated);
            writer.WriteStartArray("identifierOccurrences");
            foreach(var occurrence in validation.IdentifierOccurrences) {
                checkpoint();
                writer.WriteStartObject();
                writer.WriteString("identifier", occurrence.Identifier);
                WriteSpan(writer, occurrence.Span);
                writer.WriteString("role", SpdxReportValues.Role(occurrence.Role));
                writer.WriteString("lookupStatus", SpdxReportValues.Lookup(occurrence.LookupStatus));
                writer.WriteString("canonicalIdentifier", occurrence.CanonicalIdentifier);
                if(occurrence.IsDeprecated is { } deprecated)
                    writer.WriteBoolean("isDeprecated", deprecated);
                else
                    writer.WriteNull("isDeprecated");
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("references");
            foreach(var reference in validation.References) {
                checkpoint();
                writer.WriteStartObject();
                writer.WriteString("documentId", reference.DocumentId);
                writer.WriteString("licenseId", reference.LicenseId);
                WriteSpan(writer, reference.Span);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            WriteDiagnostics(writer, "diagnostics", validation.Diagnostics, checkpoint);
            writer.WriteEndObject();
        }
        internal static void WriteDiagnostics(Utf8JsonWriter writer, string name, IReadOnlyList<SpdxDiagnostic> diagnostics, Action checkpoint) {
            writer.WriteStartArray(name);
            foreach(var diagnostic in diagnostics) {
                checkpoint();
                writer.WriteStartObject();
                writer.WriteString("code", diagnostic.Code);
                writer.WriteString("severity", SpdxReportValues.Severity(diagnostic.Severity));
                WriteSpan(writer, diagnostic.Span);
                writer.WriteString("message", diagnostic.Message);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        static void WriteSpan(Utf8JsonWriter writer, SpdxSourceSpan span) {
            writer.WriteStartObject("span");
            writer.WriteNumber("start", span.Start);
            writer.WriteNumber("length", span.Length);
            writer.WriteEndObject();
        }
    }
}
