namespace Fonts.Metadata {
    /// <summary>Describes completeness of a metadata inspection.</summary>
    public enum FontReadStatus {
        /// <summary>No recognized leading font signature.</summary>
        NotRecognized,
        /// <summary>All supported metadata was processed without loss.</summary>
        Complete,
        /// <summary>Verified metadata survives an error, unsupported feature or resource limit.</summary>
        Partial,
        /// <summary>Reading failed without usable metadata.</summary>
        Failed,
        /// <summary>The recognized format or feature is unavailable and no usable metadata was read.</summary>
        Unsupported,
    }
}
