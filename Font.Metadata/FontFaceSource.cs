namespace Fonts.Metadata {
    /// <summary>Identifies the container component from which a face was read.</summary>
    public enum FontFaceSource {
        /// <summary>A face from SFNT, TTC, WOFF or WOFF2.</summary>
        Container = 0,
        /// <summary>The main EOT font payload, with face index zero.</summary>
        EotMain = 1,
        /// <summary>The separate EOT EUDC payload, with face index one.</summary>
        EotEudc = 2
    }
}
