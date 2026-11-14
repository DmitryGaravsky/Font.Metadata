namespace Fonts.Metadata {
    /// <summary>A stable adapter work-limit code with the original triggering record.</summary>
    public sealed class FontSpdxDiagnostic {
        internal FontSpdxDiagnostic(string code, string message, FontMetadata.NameRecord record) {
            Code = code;
            Message = message;
            SourceRecord = record;
        }
        /// <summary>FSI_SOURCE_RECORD_LIMIT, FSI_SOURCE_CHARACTER_LIMIT, FSI_DECLARATION_LIMIT or FSI_PROBE_PARSE_LIMIT.</summary>
        public string Code { get; }
        /// <summary>Human-readable English explanation; not a machine contract.</summary>
        public string Message { get; }
        /// <summary>Original record whose work or admitted result could not be completed or retained.</summary>
        public FontMetadata.NameRecord SourceRecord { get; }
    }
}
