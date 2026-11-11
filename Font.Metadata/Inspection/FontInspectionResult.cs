using System.Collections.Generic;

namespace Fonts.Metadata {
    /// <summary>Immutable inspection outcome, preserving verified metadata even when processing is incomplete.</summary>
    public sealed class FontInspectionResult {
        internal FontInspectionResult(FontReadStatus status, FontMetadata? metadata, IReadOnlyList<FontDiagnostic> diagnostics) {
            Status = status;
            Metadata = metadata;
            Diagnostics = diagnostics;
        }
        /// <summary>Overall completeness, independent of individual table statuses.</summary>
        public FontReadStatus Status { get; }
        /// <summary>Available metadata, or null when no source format could be recognized.</summary>
        public FontMetadata? Metadata { get; }
        /// <summary>Ordered diagnostics in an immutable collection.</summary>
        public IReadOnlyList<FontDiagnostic> Diagnostics { get; }
    }
}
