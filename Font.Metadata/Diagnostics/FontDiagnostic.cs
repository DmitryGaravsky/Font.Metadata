namespace Fonts.Metadata {
    /// <summary>An immutable observation located relative to the supplied input slice or stream position.</summary>
    public sealed class FontDiagnostic {
        internal FontDiagnostic(string code, FontDiagnosticSeverity severity, FontDiagnosticCategory category,
            string message, int? faceIndex = null, string? tableTag = null, long? byteOffset = null) {
            Code = code;
            Severity = severity;
            Category = category;
            Message = message;
            FaceIndex = faceIndex;
            TableTag = tableTag;
            ByteOffset = byteOffset;
        }
        /// <summary>Stable identifier from FontDiagnosticCodes.</summary>
        public string Code { get; }
        /// <summary>Diagnostic severity.</summary>
        public FontDiagnosticSeverity Severity { get; }
        /// <summary>Machine-readable failure category.</summary>
        public FontDiagnosticCategory Category { get; }
        /// <summary>Human-readable explanation; do not parse this text.</summary>
        public string Message { get; }
        /// <summary>Original collection face index, or null for container-wide observations.</summary>
        public int? FaceIndex { get; }
        /// <summary>Four-character table tag, or null for a container or face directory.</summary>
        public string? TableTag { get; }
        /// <summary>Byte offset relative to the supplied input origin, when known.</summary>
        public long? ByteOffset { get; }
        internal FontDiagnostic ForFace(int index) {
            return new FontDiagnostic(Code, Severity, Category, Message, index, TableTag, ByteOffset);
        }
    }
}
