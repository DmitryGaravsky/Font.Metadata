namespace Fonts.Metadata {
    /// <summary>Identifies the source container, independently of its outline technology.</summary>
    public enum FontFormat {
        /// <summary>No recognized container signature.</summary>
        Unknown,
        /// <summary>A standalone TrueType or OpenType font.</summary>
        Sfnt,
        /// <summary>A TTC or OTC collection.</summary>
        Collection,
        /// <summary>WOFF; recognized but not yet supported.</summary>
        Woff,
        /// <summary>WOFF2; recognized but not yet supported.</summary>
        Woff2,
        /// <summary>Embedded OpenType; recognized but not yet supported.</summary>
        Eot,
    }
}
