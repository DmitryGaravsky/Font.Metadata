namespace Fonts.Metadata {
    /// <summary>Verified raw fields from an OpenType head table.</summary>
    public sealed class FontHeader {
        internal FontHeader(int revision, long created, long modified, ushort unitsPerEm, ushort flags, ushort macStyle) {
            FontRevision = revision;
            Created = created;
            Modified = modified;
            UnitsPerEm = unitsPerEm;
            Flags = flags;
            MacStyle = macStyle;
        }
        /// <summary>Signed 16.16 Fixed revision, in units of 1/65536.</summary>
        public int FontRevision { get; }
        /// <summary>Creation time in seconds relative to 1904-01-01 UTC.</summary>
        public long Created { get; }
        /// <summary>Modification time in seconds relative to 1904-01-01 UTC.</summary>
        public long Modified { get; }
        /// <summary>Design units per em, as recorded.</summary>
        public ushort UnitsPerEm { get; }
        /// <summary>Raw head flags.</summary>
        public ushort Flags { get; }
        /// <summary>Raw macStyle bits.</summary>
        public ushort MacStyle { get; }
    }
}
