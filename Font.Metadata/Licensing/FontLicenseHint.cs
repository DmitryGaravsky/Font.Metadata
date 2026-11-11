namespace Fonts.Metadata {
    /// <summary>An immutable license indication with its exact source record. It does not establish usage rights.</summary>
    public sealed class FontLicenseHint {
        internal FontLicenseHint(string identifier, FontLicenseIdentifierKind identifierKind,
            string ruleId, FontMetadata.NameRecord sourceRecord) {
            Identifier = identifier;
            IdentifierKind = identifierKind;
            RuleId = ruleId;
            SourceRecord = sourceRecord;
        }
        /// <summary>Canonical supported SPDX identifier or stable family name; never an AND/OR/WITH expression.</summary>
        public string Identifier { get; }
        /// <summary>Whether the indication identifies an individual license or an unspecified family.</summary>
        public FontLicenseIdentifierKind IdentifierKind { get; }
        /// <summary>Stable evidence rule: FLA001 (ID), FLA002 (name), FLA003 (family), or FLA004 (URL).</summary>
        public string RuleId { get; }
        /// <summary>The original record object from the face, with its unmodified decoded value and index.</summary>
        public FontMetadata.NameRecord SourceRecord { get; }
    }
}
