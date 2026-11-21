using System;

namespace Fonts.Metadata {
    /// <summary>Immutable reader settings. Each operation has independent mutable processing state.</summary>
    public sealed class FontReadOptions {
        /// <summary>Resource limits, shared safely because their properties are init-only.</summary>
        public FontReadLimits Limits { get; init; } = new FontReadLimits();
        /// <summary>Read Unicode scalar ranges from a supported cmap subtable for each face.</summary>
        public bool IncludeUnicodeCoverage { get; init; }
        /// <summary>Read design axes from the fvar table of each face.</summary>
        public bool IncludeVariationAxes { get; init; }
        internal FontReadLimits Validate() {
            ArgumentNullException.ThrowIfNull(Limits);
            Limits.Validate();
            return Limits;
        }
    }
}
