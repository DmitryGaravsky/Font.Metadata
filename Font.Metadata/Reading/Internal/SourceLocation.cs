namespace Fonts.Metadata.Internal {
    // Decoded coordinates are never added to a compressed input anchor.
    internal readonly record struct SourceLocation(int Id, long? Offset, bool Compressed = false) {
        internal long? Map(long relative) {
            return Compressed ? Offset : Offset + relative;
        }
        internal SourceLocation At(long relative) {
            long? offset = Map(relative);
            return new SourceLocation(Id, offset, Compressed);
        }
        public static implicit operator SourceLocation(int offset) {
            return new SourceLocation(0, offset);
        }
    }
}
