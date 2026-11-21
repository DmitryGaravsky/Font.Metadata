using System;

namespace Fonts.Metadata {
    public sealed partial class FontReadLimits {
        const int DefaultMaxTotalCmapWork = 1048576;
        const int DefaultMaxTotalUnicodeRanges = 65536;
        /// <summary>Maximum cmap encoding records, groups, segments and glyph-array entries examined across the container.</summary>
        public int MaxTotalCmapWork { get; init; } = DefaultMaxTotalCmapWork;
        /// <summary>Maximum compact Unicode coverage ranges retained across distinct cmap tables in the container.</summary>
        public int MaxTotalUnicodeRanges { get; init; } = DefaultMaxTotalUnicodeRanges;
        void ValidateUnicodeCoverageLimits() {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxTotalCmapWork, nameof(MaxTotalCmapWork));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxTotalUnicodeRanges, nameof(MaxTotalUnicodeRanges));
        }
    }
}
