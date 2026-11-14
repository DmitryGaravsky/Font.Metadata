using System;
using System.Collections.Generic;
using System.Threading;
using Spdx.Expressions;

namespace Fonts.Metadata {
    /// <summary>Offline, source-preserving expression analysis, independent of reading and legacy license hints.</summary>
    public static class FontSpdxAnalyzer {
        const int LicenseNameId = 13;
        const string Prefix = "SPDX-License-Identifier:";
        const string RecordLimitMessage = "The source record limit was reached before this record.";
        const string CharacterLimitMessage = "The source character limit was reached before this record.";
        const string DeclarationLimitMessage = "The declaration limit omitted this admitted attempt and subsequent work.";
        const string ProbeLimitMessage = "A bare expression probe exhausted a parser limit; recognition is incomplete.";

        /// <summary>Analyzes ID 13 records in source order using the font-name13-v1 whole-record profile.</summary>
        /// <exception cref="ArgumentNullException">Face, registry or an explicitly supplied nested option is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A limit is outside its documented range.</exception>
        /// <exception cref="OperationCanceledException">Cancellation was requested; no partial result is returned.</exception>
        public static FontSpdxAnalysis Analyze(FontMetadata.Face face, SpdxLicenseRegistry registry,
            FontSpdxOptions? options = null, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(face);
            ArgumentNullException.ThrowIfNull(registry);
            options ??= new FontSpdxOptions();
            options.Check();
            cancellationToken.ThrowIfCancellationRequested();
            var declarations = new List<FontSpdxDeclaration>();
            var diagnostics = new List<FontSpdxDiagnostic>();
            bool complete = true, truncated = false;
            int visited = 0, characters = 0;
            foreach(var record in face.NameRecords) {
                cancellationToken.ThrowIfCancellationRequested();
                if(visited == options.MaxSourceRecords) {
                    Limit("FSI_SOURCE_RECORD_LIMIT", RecordLimitMessage, record);
                    break;
                }
                visited++;
                if(record.NameId != LicenseNameId)
                    continue;
                string value = record.Value;
                if(value.Length > options.MaxTotalSourceCharacters - characters) {
                    Limit("FSI_SOURCE_CHARACTER_LIMIT", CharacterLimitMessage, record);
                    break;
                }
                characters += value.Length;
                int start = 0, end = value.Length;
                Trim(value, ref start, ref end, cancellationToken);
                if(start == end)
                    continue;
                bool explicitAnnotation = value.AsSpan(start, end - start).StartsWith(Prefix, StringComparison.Ordinal);
                if(explicitAnnotation) {
                    start += Prefix.Length;
                    int suffixStart = start;
                    Trim(value, ref start, ref end, cancellationToken);
                    if(start == end)
                        start = end = suffixStart;
                }
                string text = value.Substring(start, end - start);
                var parsed = SpdxExpressionParser.Parse(text, options.ParseOptions, cancellationToken);
                SpdxValidationResult? validated = parsed.Expression is { } expression ?
                    SpdxExpressionValidator.Validate(expression, registry, options.ValidationOptions, cancellationToken) : null;
                if(parsed.Status == SpdxParseStatus.LimitExceeded) {
                    complete = false;
                    if(!explicitAnnotation)
                        Limit("FSI_PROBE_PARSE_LIMIT", ProbeLimitMessage, record);
                }
                if(!explicitAnnotation && !AdmitBare(validated, cancellationToken))
                    continue;
                if(declarations.Count == options.MaxDeclarations) {
                    Limit("FSI_DECLARATION_LIMIT", DeclarationLimitMessage, record);
                    break;
                }
                declarations.Add(new FontSpdxDeclaration(record, explicitAnnotation ? "FSI001" : "FSI002", start, text, parsed, validated));
            }
            cancellationToken.ThrowIfCancellationRequested();
            bool sourceComplete = face.NameTableStatus is FontSectionStatus.Complete or FontSectionStatus.Missing;
            return new FontSpdxAnalysis(sourceComplete, complete, registry.Identity, declarations, diagnostics, truncated);

            void Limit(string code, string message, FontMetadata.NameRecord record) {
                complete = false;
                if(diagnostics.Count < options.MaxDiagnostics)
                    diagnostics.Add(new FontSpdxDiagnostic(code, message, record));
                else
                    truncated = true;
            }
        }
        static bool AdmitBare(SpdxValidationResult? result, CancellationToken token) {
            if(result == null)
                return false;
            if(result.HasUnresolvedReferences)
                return true;
            foreach(var occurrence in result.IdentifierOccurrences) {
                token.ThrowIfCancellationRequested();
                if(occurrence.Role == SpdxIdentifierRole.License && occurrence.LookupStatus == SpdxIdentifierLookupStatus.Known)
                    return true;
            }
            return false;
        }
        static void Trim(string text, ref int start, ref int end, CancellationToken token) {
            while(start < end && IsSpace(text[start])) {
                token.ThrowIfCancellationRequested();
                start++;
            }
            while(end > start && IsSpace(text[end - 1])) {
                token.ThrowIfCancellationRequested();
                end--;
            }
        }
        static bool IsSpace(char value) {
            return value is ' ' or '\t' or '\r' or '\n';
        }
    }
}
