using System.Collections.Generic;
using System.Collections.ObjectModel;

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
        /// <summary>Available container metadata, or null when unavailable.</summary>
        public FontMetadata? Metadata { get; }
        /// <summary>Faces in source order, or an empty read-only collection when metadata is unavailable.</summary>
        /// <remarks>
        /// Preserves the original faces, including unreadable-face placeholders. An empty collection does not
        /// imply successful reading or absent container metadata; inspect Status and Diagnostics for the outcome.
        /// </remarks>
        public IReadOnlyList<FontMetadata.Face> Faces {
            get { return Metadata?.Faces ?? ReadOnlyCollection<FontMetadata.Face>.Empty; }
        }
        /// <summary>Ordered diagnostics in an immutable collection.</summary>
        public IReadOnlyList<FontDiagnostic> Diagnostics { get; }
    }
}
