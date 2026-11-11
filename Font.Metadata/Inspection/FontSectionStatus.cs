namespace Fonts.Metadata {
    /// <summary>Describes one metadata table independently of the overall result.</summary>
    public enum FontSectionStatus {
        /// <summary>Reading did not start, for example because its directory or budget is unavailable.</summary>
        NotRead,
        /// <summary>A complete directory proves the table is absent.</summary>
        Missing,
        /// <summary>All metadata supported by this reader was processed.</summary>
        Complete,
        /// <summary>Some verified values survive incomplete processing.</summary>
        Partial,
        /// <summary>Malformed data prevented extraction of any verified values.</summary>
        Invalid,
        /// <summary>An unsupported version or encoding prevented extraction of any values.</summary>
        Unsupported,
    }
}
