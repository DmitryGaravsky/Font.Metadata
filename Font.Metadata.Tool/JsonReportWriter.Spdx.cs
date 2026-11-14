using System.Collections.Generic;
using Spdx.Expressions;

namespace Fonts.Metadata.Tool {
    internal sealed partial class JsonReportWriter {
        void WriteSpdxContext() {
            var identity = registry!.Identity;
            writer.WriteStartObject("spdxContext");
            writer.WriteString("recognitionProfile", "font-name13-v1");
            writer.WriteString("syntaxProfile", "SPDX-2.3.1");
            writer.WriteStartObject("registry");
            writer.WriteString("licenseListVersion", identity.LicenseListVersion);
            writer.WriteString("licensesSha256", identity.LicensesSha256);
            writer.WriteString("exceptionsSha256", identity.ExceptionsSha256);
            writer.WriteEndObject();
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
                WriteSpdxDiagnostics("parseDiagnostics", declaration.ParseResult.Diagnostics);
                writer.WritePropertyName("validation");
                if(declaration.ValidationResult is { } validation)
                    WriteValidation(validation);
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
        void WriteValidation(SpdxValidationResult validation) {
            writer.WriteStartObject();
            writer.WriteBoolean("hasIdentifierErrors", validation.HasIdentifierErrors);
            writer.WriteBoolean("hasUnresolvedReferences", validation.HasUnresolvedReferences);
            writer.WriteBoolean("diagnosticsTruncated", validation.DiagnosticsTruncated);
            writer.WriteStartArray("identifierOccurrences");
            foreach(var occurrence in validation.IdentifierOccurrences) {
                Checkpoint();
                writer.WriteStartObject();
                writer.WriteString("identifier", occurrence.Identifier);
                WriteSpan(occurrence.Span);
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
                Checkpoint();
                writer.WriteStartObject();
                writer.WriteString("documentId", reference.DocumentId);
                writer.WriteString("licenseId", reference.LicenseId);
                WriteSpan(reference.Span);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            WriteSpdxDiagnostics("diagnostics", validation.Diagnostics);
            writer.WriteEndObject();
        }
        void WriteSpdxDiagnostics(string name, IReadOnlyList<SpdxDiagnostic> diagnostics) {
            writer.WriteStartArray(name);
            foreach(var diagnostic in diagnostics) {
                Checkpoint();
                writer.WriteStartObject();
                writer.WriteString("code", diagnostic.Code);
                writer.WriteString("severity", SpdxReportValues.Severity(diagnostic.Severity));
                WriteSpan(diagnostic.Span);
                writer.WriteString("message", diagnostic.Message);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        void WriteSpan(SpdxSourceSpan span) {
            writer.WriteStartObject("span");
            writer.WriteNumber("start", span.Start);
            writer.WriteNumber("length", span.Length);
            writer.WriteEndObject();
        }
    }
}
