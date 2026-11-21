using System.Collections.Generic;
using Fonts.Metadata.Internal;
using Spdx.Expressions;

namespace Fonts.Metadata {
    /// <summary>Evidence found in one caller-supplied document.</summary>
    public sealed class FontLicenseDocumentAnalysis {
        internal FontLicenseDocumentAnalysis(bool complete, SpdxRegistryIdentity registry,
            List<FontLicenseDocumentEvidence> evidence, FontLicenseDocumentDiagnostic? diagnostic) {
            IsAnalysisComplete = complete;
            RegistryIdentity = registry;
            Evidence = ResultList<FontLicenseDocumentEvidence>.Snapshot(evidence);
            Diagnostic = diagnostic;
        }
        /// <summary>Whether document recognition and SPDX parsing completed within their limits.</summary>
        /// <remarks>Syntax errors, unknown identifiers and truncated validation diagnostics can occur in complete analyses.
        /// Each declaration's validation result reports diagnostic truncation independently.</remarks>
        public bool IsAnalysisComplete { get; }
        /// <summary>Identity of the registry used to validate explicit SPDX expressions.</summary>
        public SpdxRegistryIdentity RegistryIdentity { get; }
        /// <summary>Entries in source order. An empty list can occur in a complete analysis.</summary>
        public IReadOnlyList<FontLicenseDocumentEvidence> Evidence { get; }
        /// <summary>The document-level work-limit diagnostic, when present. Native diagnostics belong to each SPDX entry.</summary>
        public FontLicenseDocumentDiagnostic? Diagnostic { get; }
    }
    /// <summary>A work limit reached while inspecting the document.</summary>
    public sealed class FontLicenseDocumentDiagnostic {
        internal FontLicenseDocumentDiagnostic(string code, string message, int? sourceStart = null, int? sourceLength = null) {
            Code = code;
            Message = message;
            SourceStart = sourceStart;
            SourceLength = sourceLength;
        }
        /// <summary>FDL_SOURCE_CHARACTER_LIMIT or FDL_EVIDENCE_LIMIT.</summary>
        public string Code { get; }
        /// <summary>English explanation for display.</summary>
        public string Message { get; }
        /// <summary>Zero-based UTF-16 offset of the first omitted entry, when known.</summary>
        public int? SourceStart { get; }
        /// <summary>UTF-16 length of the first omitted entry, when known.</summary>
        public int? SourceLength { get; }
    }
}
