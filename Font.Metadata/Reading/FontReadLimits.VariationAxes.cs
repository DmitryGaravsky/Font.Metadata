using System;

namespace Fonts.Metadata {
    public sealed partial class FontReadLimits {
        /// <summary>Maximum fvar axis records processed across the container, including invalid records. Shared tables are counted once.</summary>
        public int MaxTotalVariationAxes { get; init; } = 16384;
        internal void ValidateVariationAxisLimits() {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxTotalVariationAxes, nameof(MaxTotalVariationAxes));
        }
    }
}
