using Spdx.Expressions;

namespace Fonts.Metadata {
    /// <summary>An immutable declaration attempt linked to its exact decoded source record.</summary>
    public sealed class FontSpdxDeclaration {
        internal FontSpdxDeclaration(FontMetadata.NameRecord source, string rule, int start, string text,
            SpdxParseResult parsed, SpdxValidationResult? validated) {
            SourceRecord = source;
            RuleId = rule;
            SourceStart = start;
            ExpressionText = text;
            ParseResult = parsed;
            ValidationResult = validated;
        }
        /// <summary>The original record object, without normalization or reconstruction.</summary>
        public FontMetadata.NameRecord SourceRecord { get; }
        /// <summary>FSI001 for an explicit annotation; FSI002 for an admitted bare expression.</summary>
        public string RuleId { get; }
        /// <summary>UTF-16 start in SourceRecord.Value, after prefix and outer ASCII whitespace. An empty explicit suffix starts immediately after the colon.</summary>
        public int SourceStart { get; }
        /// <summary>UTF-16 substring length. Native result spans are relative to this substring.</summary>
        public int SourceLength => ExpressionText.Length;
        /// <summary>Exact source substring supplied to the parser, including empty explicit attempts.</summary>
        public string ExpressionText { get; }
        /// <summary>Native parse outcome and immutable AST, without a translated or flattened copy.</summary>
        public SpdxParseResult ParseResult { get; }
        /// <summary>Native validation evidence on successful parsing only; otherwise null.</summary>
        public SpdxValidationResult? ValidationResult { get; }
    }
}
