using System;
using System.Collections.Generic;
using System.Threading;
using Spdx.Expressions;

namespace Fonts.Metadata {
    /// <summary>Offline analysis of caller-supplied license document text.</summary>
    public static class FontLicenseDocumentAnalyzer {
        internal const string DeclarationPrefix = "SPDX-License-Identifier:";
        const string CharacterLimitMessage = "The document exceeds the UTF-16 input length limit.";
        const string EvidenceLimitMessage = "The document evidence limit omitted this entry and subsequent entries.";
        /// <summary>Inspects the original UTF-16 text using the built-in or caller-supplied license document catalog.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="registry"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A supplied limit is less than one.</exception>
        /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
        public static FontLicenseDocumentAnalysis Analyze(string text, SpdxLicenseRegistry registry,
            FontLicenseDocumentOptions? options = null, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(registry);
            options ??= new FontLicenseDocumentOptions();
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxSourceCharacters);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxEvidence);
            cancellationToken.ThrowIfCancellationRequested();
            var evidence = new List<FontLicenseDocumentEvidence>();
            if(text.Length > options.MaxSourceCharacters) {
                var tooLong = new FontLicenseDocumentDiagnostic("FDL_SOURCE_CHARACTER_LIMIT", CharacterLimitMessage);
                return new FontLicenseDocumentAnalysis(false, registry.Identity, evidence, tooLong);
            }
            var body = new LicenseDocumentBody(options.Catalog ?? FontLicenseDocumentCatalog.BuiltIn);
            FontLicenseDocumentDiagnostic? diagnostic = null;
            bool complete = true;
            int next = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
            while(next < text.Length) {
                cancellationToken.ThrowIfCancellationRequested();
                int lineStart = next, lineEnd = next;
                while(lineEnd < text.Length && text[lineEnd] is not ('\r' or '\n')) {
                    if((lineEnd & 1023) == 0)
                        cancellationToken.ThrowIfCancellationRequested();
                    lineEnd++;
                }
                next = lineEnd;
                if(next < text.Length && text[next++] == '\r' && next < text.Length && text[next] == '\n')
                    next++;
                int start = lineStart, end = lineEnd;
                TrimHorizontal(text, ref start, ref end, cancellationToken);
                if(start == end)
                    continue;
                bool explicitDeclaration = text.AsSpan(start, end - start).StartsWith(DeclarationPrefix, StringComparison.Ordinal);
                var heading = explicitDeclaration ? null : body.ReadHeading(text, start, ref end, ref next, cancellationToken);
                body.Observe(text, start, ref end, ref next, heading != null, explicitDeclaration, cancellationToken);
                if(explicitDeclaration) {
                    if(!HasRoom(lineStart, lineEnd))
                        break;
                    var declaration = ReadDeclaration(text, lineStart, lineEnd, start + DeclarationPrefix.Length, registry, cancellationToken);
                    complete &= declaration.ParseResult!.Status != SpdxParseStatus.LimitExceeded;
                    evidence.Add(declaration);
                }
                else if(heading != null) {
                    if(!HasRoom(start, end))
                        break;
                    evidence.Add(new FontLicenseDocumentEvidence(FontLicenseDocumentOrigin.Header, heading.Identifier, start, text[start..end]));
                }
            }
            if(diagnostic == null && body.Match(text, cancellationToken) is { } match && HasRoom(match.Start, match.End))
                evidence.Add(new FontLicenseDocumentEvidence(FontLicenseDocumentOrigin.FullText, match.Identifier,
                    match.Start, text[match.Start..match.End]));
            cancellationToken.ThrowIfCancellationRequested();
            return new FontLicenseDocumentAnalysis(complete && diagnostic == null, registry.Identity, evidence, diagnostic);
            bool HasRoom(int start, int end) {
                if(evidence.Count < options.MaxEvidence)
                    return true;
                diagnostic = new FontLicenseDocumentDiagnostic("FDL_EVIDENCE_LIMIT", EvidenceLimitMessage, start, end - start);
                return false;
            }
        }
        static FontLicenseDocumentEvidence ReadDeclaration(string text, int lineStart, int lineEnd, int suffixStart,
            SpdxLicenseRegistry registry, CancellationToken token) {
            int start = suffixStart, end = lineEnd;
            TrimHorizontal(text, ref start, ref end, token);
            if(start == end)
                start = end = suffixStart;
            string expressionText = text[start..end];
            var parsed = SpdxExpressionParser.Parse(expressionText, cancellationToken: token);
            var validation = parsed.Expression is { } expression ?
                SpdxExpressionValidator.Validate(expression, registry, cancellationToken: token) : null;
            return new FontLicenseDocumentEvidence(FontLicenseDocumentOrigin.SpdxDeclaration, null, lineStart,
                text[lineStart..lineEnd], start, expressionText, parsed, validation);
        }
        static void TrimHorizontal(string text, ref int start, ref int end, CancellationToken token) {
            while(start < end && text[start] is ' ' or '\t') {
                if((start & 1023) == 0)
                    token.ThrowIfCancellationRequested();
                start++;
            }
            while(end > start && text[end - 1] is ' ' or '\t') {
                if((end & 1023) == 0)
                    token.ThrowIfCancellationRequested();
                end--;
            }
        }
    }
}
