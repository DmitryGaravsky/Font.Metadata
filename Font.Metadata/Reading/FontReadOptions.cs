using System;

namespace Fonts.Metadata {
    /// <summary>Immutable reader settings. Each operation has independent mutable processing state.</summary>
    public sealed class FontReadOptions {
        /// <summary>Resource limits, shared safely because their properties are init-only.</summary>
        public FontReadLimits Limits { get; init; } = new FontReadLimits();
        internal FontReadLimits Validate() {
            ArgumentNullException.ThrowIfNull(Limits);
            Limits.Validate();
            return Limits;
        }
    }
}
