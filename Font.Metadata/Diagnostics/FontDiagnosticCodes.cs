namespace Fonts.Metadata {
    /// <summary>Stable diagnostic identifiers. Message text is not a machine-readable contract.</summary>
    public static class FontDiagnosticCodes {
        /// <summary>An SFNT directory or table range is invalid.</summary>
        public const string InvalidDirectory = "FM_INVALID_DIRECTORY";
        /// <summary>A TTC header or face directory reference is invalid.</summary>
        public const string InvalidCollection = "FM_INVALID_COLLECTION";
        /// <summary>A name structure, string or language tag is invalid.</summary>
        public const string InvalidNameTable = "FM_INVALID_NAME_TABLE";
        /// <summary>An OS/2 metadata field is invalid or truncated.</summary>
        public const string InvalidOs2Table = "FM_INVALID_OS2_TABLE";
        /// <summary>A recognized container or SFNT flavor is not supported.</summary>
        public const string UnsupportedFormat = "FM_UNSUPPORTED_FORMAT";
        /// <summary>A table or collection version is not supported.</summary>
        public const string UnsupportedTableVersion = "FM_UNSUPPORTED_TABLE_VERSION";
        /// <summary>A record uses an unsupported platform or encoding.</summary>
        public const string UnsupportedEncoding = "FM_UNSUPPORTED_ENCODING";
        /// <summary>A configured processing limit was reached.</summary>
        public const string LimitExceeded = "FM_LIMIT_EXCEEDED";
        /// <summary>Unsorted records or unaligned offsets were safely accepted.</summary>
        public const string NonConformantDirectory = "FM_NONCONFORMANT_DIRECTORY";
        /// <summary>Additional diagnostics were suppressed by the configured limit.</summary>
        public const string DiagnosticsTruncated = "FM_DIAGNOSTICS_TRUNCATED";
    }
}
