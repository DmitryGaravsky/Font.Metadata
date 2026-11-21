using Spdx.Expressions;

namespace Fonts.Metadata {
    /// <summary>How a document fragment was recognized.</summary>
    public enum FontLicenseDocumentOrigin {
        /// <summary>A line explicitly declaring an SPDX expression.</summary>
        SpdxDeclaration = 0,
        /// <summary>A recognized license heading or introductory statement.</summary>
        Header = 1,
        /// <summary>The complete normative body of a supported license text.</summary>
        FullText = 2
    }
    /// <summary>One exact source fragment and its independent recognition evidence.</summary>
    public sealed class FontLicenseDocumentEvidence {
        internal FontLicenseDocumentEvidence(FontLicenseDocumentOrigin origin, string? identifier, int sourceStart, string sourceText,
            int? expressionStart = null, string? expressionText = null, SpdxParseResult? parsed = null,
            SpdxValidationResult? validation = null) {
            Origin = origin;
            Identifier = identifier;
            SourceStart = sourceStart;
            SourceText = sourceText;
            ExpressionStart = expressionStart;
            ExpressionText = expressionText;
            ParseResult = parsed;
            ValidationResult = validation;
        }
        /// <summary>How the fragment was recognized.</summary>
        public FontLicenseDocumentOrigin Origin { get; }
        /// <summary>Identifier asserted by a recognized header or matched body, if known. Null for SPDX expressions.</summary>
        public string? Identifier { get; }
        /// <summary>Zero-based UTF-16 offset in the supplied string, including a leading BOM if present.</summary>
        public int SourceStart { get; }
        /// <summary>UTF-16 length of the source fragment.</summary>
        public int SourceLength { get { return SourceText.Length; } }
        /// <summary>Verbatim declaration line, header, or normative body, with its original interior whitespace.</summary>
        public string SourceText { get; }
        /// <summary>Zero-based UTF-16 offset in the document of the SPDX expression, or null for other origins.</summary>
        public int? ExpressionStart { get; }
        /// <summary>UTF-16 length of the expression, or null for other origins.</summary>
        public int? ExpressionLength { get { return ExpressionText?.Length; } }
        /// <summary>Exact SPDX parser input after trimming spaces and tabs from the suffix, or null for other origins.</summary>
        public string? ExpressionText { get; }
        /// <summary>Native parse outcome for a declaration. Native spans are relative to <see cref="ExpressionText"/>.</summary>
        public SpdxParseResult? ParseResult { get; }
        /// <summary>Native validation evidence on successful parsing. Native spans are relative to <see cref="ExpressionText"/>.</summary>
        public SpdxValidationResult? ValidationResult { get; }
    }
}
