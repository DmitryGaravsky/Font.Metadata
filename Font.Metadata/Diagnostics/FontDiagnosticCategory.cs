namespace Fonts.Metadata {
    /// <summary>Classifies a diagnostic independently of its human-readable message.</summary>
    public enum FontDiagnosticCategory {
        /// <summary>Invalid structure, ranges or encoded data.</summary>
        MalformedData,
        /// <summary>A configured resource budget was exhausted.</summary>
        LimitExceeded,
        /// <summary>A recognized format or table version is unavailable.</summary>
        UnsupportedFeature,
        /// <summary>A name encoding is unavailable.</summary>
        UnsupportedEncoding,
        /// <summary>A recoverable ordering or alignment deviation.</summary>
        NonConformantData,
    }
}
