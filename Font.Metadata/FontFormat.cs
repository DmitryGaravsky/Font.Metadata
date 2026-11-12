namespace Fonts.Metadata {
    /// <summary>Identifies the source container, independently of its outline technology.</summary>
    public enum FontFormat {
        /// <summary>No recognized container signature.</summary>
        Unknown,
        /// <summary>A standalone TrueType or OpenType font.</summary>
        Sfnt,
        /// <summary>A TTC or OTC collection.</summary>
        Collection,
        /// <summary>WOFF metadata container.</summary>
        Woff,
        /// <summary>WOFF2 metadata container.</summary>
        Woff2,
        /// <summary>Embedded OpenType metadata container.</summary>
        Eot,
    }
}
