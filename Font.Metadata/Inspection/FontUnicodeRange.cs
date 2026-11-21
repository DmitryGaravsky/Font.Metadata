namespace Fonts.Metadata {
    /// <summary>An inclusive range of Unicode scalar values covered by a cmap subtable.</summary>
    public readonly record struct FontUnicodeRange {
        internal FontUnicodeRange(int start, int end) {
            Start = start;
            End = end;
        }
        /// <summary>The first Unicode scalar value in the range.</summary>
        public int Start { get; }
        /// <summary>The last Unicode scalar value in the range.</summary>
        public int End { get; }
    }
}
