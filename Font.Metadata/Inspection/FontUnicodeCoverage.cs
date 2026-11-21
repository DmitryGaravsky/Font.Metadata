using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Fonts.Metadata {
    /// <summary>Unicode scalar coverage from one selected cmap subtable.</summary>
    public sealed class FontUnicodeCoverage {
        readonly ReadOnlyCollection<FontUnicodeRange> ranges = ReadOnlyCollection<FontUnicodeRange>.Empty;
        internal FontUnicodeCoverage(FontSectionStatus status, ReadOnlyCollection<FontDiagnostic>? diagnostics = null) {
            Status = status;
            Diagnostics = diagnostics ?? ReadOnlyCollection<FontDiagnostic>.Empty;
        }
        internal FontUnicodeCoverage(FontSectionStatus status, ushort? format, ushort? platformId, ushort? encodingId,
            ReadOnlyCollection<FontUnicodeRange> ranges, IReadOnlyList<FontDiagnostic> diagnostics) {
            Status = status;
            Format = format;
            PlatformId = platformId;
            EncodingId = encodingId;
            this.ranges = ranges;
            Diagnostics = diagnostics;
        }
        /// <summary>Completion status for this coverage.</summary>
        public FontSectionStatus Status { get; }
        /// <summary>The selected cmap subtable format, or null when none was selected.</summary>
        public ushort? Format { get; }
        /// <summary>The selected encoding record's platform ID, or null when none was selected.</summary>
        public ushort? PlatformId { get; }
        /// <summary>The selected encoding record's encoding ID, or null when none was selected.</summary>
        public ushort? EncodingId { get; }
        /// <summary>Sorted, disjoint inclusive scalar ranges with nonzero glyph IDs. Adjacent ranges are merged.</summary>
        public IReadOnlyList<FontUnicodeRange> Ranges { get { return ranges; } }
        /// <summary>Diagnostics for coverage extraction, with table-relative coordinates when available.</summary>
        public IReadOnlyList<FontDiagnostic> Diagnostics { get; }
        internal FontUnicodeCoverage WithStatus(FontSectionStatus status) {
            if(Status == status)
                return this;
            return new FontUnicodeCoverage(status, Format, PlatformId, EncodingId, ranges, Diagnostics);
        }
    }
}
