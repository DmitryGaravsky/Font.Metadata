namespace Fonts.Metadata {
    /// <summary>Distinguishes a supported SPDX identifier from an incompletely specified license family.</summary>
    public enum FontLicenseIdentifierKind {
        /// <summary>A recognized individual SPDX identifier; not a license conclusion or expression.</summary>
        Spdx,
        /// <summary>A license family whose version or variant has not been established.</summary>
        Family,
    }
}
