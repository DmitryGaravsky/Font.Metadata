namespace Fonts.Metadata {
    /// <summary>Recognition catalog and inclusive limits for one external license document.</summary>
    public sealed class FontLicenseDocumentOptions {
        /// <summary>Maximum input length in UTF-16 code units, including any leading BOM. Default 1,048,576.</summary>
        public int MaxSourceCharacters { get; init; } = 1_048_576;
        /// <summary>Maximum retained evidence entries, including unsuccessful SPDX attempts. Default 256.</summary>
        public int MaxEvidence { get; init; } = 256;
        /// <summary>Catalog for this call. Null uses the built-in catalog.</summary>
        public FontLicenseDocumentCatalog? Catalog { get; init; }
    }
}
