using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Fonts.Metadata {
    /// <summary>The variation axes read from a face's OpenType fvar table, in source order.</summary>
    public sealed class FontVariationAxes {
        readonly ReadOnlyCollection<FontVariationAxis> axes;
        internal FontVariationAxes(FontSectionStatus status, bool hasTableEntry, ReadOnlyCollection<FontDiagnostic>? diagnostics = null) {
            HasTableEntry = hasTableEntry;
            Status = status;
            axes = ReadOnlyCollection<FontVariationAxis>.Empty;
            Diagnostics = diagnostics ?? ReadOnlyCollection<FontDiagnostic>.Empty;
        }
        internal FontVariationAxes(FontSectionStatus status, ReadOnlyCollection<FontVariationAxis> axes, IReadOnlyList<FontDiagnostic> diagnostics, bool hasTableEntry = true) {
            Status = status;
            HasTableEntry = hasTableEntry;
            this.axes = axes;
            Diagnostics = diagnostics;
        }
        /// <summary>True when the face directory identified an fvar entry, even if its contents could not be read.</summary>
        /// <remarks>When false, Missing means the table is absent. Other statuses mean its presence could not be established.</remarks>
        public bool HasTableEntry { get; }
        /// <summary>Reading status of the declared axis array and its source. Completeness applies to axis metadata.</summary>
        public FontSectionStatus Status { get; }
        /// <summary>Verified axes, retaining their original indexes.</summary>
        public IReadOnlyList<FontVariationAxis> Axes { get { return axes; } }
        /// <summary>Diagnostics produced while reading the axes, using table-relative coordinates where available.</summary>
        public IReadOnlyList<FontDiagnostic> Diagnostics { get; }
        internal FontVariationAxes WithStatus(FontSectionStatus status) {
            if(status == Status)
                return this;
            return new FontVariationAxes(status, axes, Diagnostics, HasTableEntry);
        }
    }
    /// <summary>One OpenType variation axis in its source user coordinate scale.</summary>
    public sealed class FontVariationAxis {
        internal FontVariationAxis(int index, string tag, decimal minimum, decimal defaultValue, decimal maximum, ushort flags, ushort nameId) {
            Index = index;
            Tag = tag;
            Minimum = minimum;
            Default = defaultValue;
            Maximum = maximum;
            Flags = flags;
            NameId = nameId;
        }
        /// <summary>Zero-based position in the source axis array.</summary>
        public int Index { get; }
        /// <summary>Case-sensitive four-character design-variation axis tag.</summary>
        public string Tag { get; }
        /// <summary>Exact minimum coordinate decoded from signed Fixed 16.16.</summary>
        public decimal Minimum { get; }
        /// <summary>Exact default coordinate decoded from signed Fixed 16.16.</summary>
        public decimal Default { get; }
        /// <summary>Exact maximum coordinate decoded from signed Fixed 16.16.</summary>
        public decimal Maximum { get; }
        /// <summary>Raw axis flags. Bit zero is the OpenType hidden-axis flag.</summary>
        public ushort Flags { get; }
        /// <summary>Source name ID for lookup among the face's name records, even if no matching record was read.</summary>
        public ushort NameId { get; }
    }
}
