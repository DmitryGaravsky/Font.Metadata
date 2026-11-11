namespace Fonts.Metadata {
    /// <summary>Indicates whether an observation prevents complete reading.</summary>
    public enum FontDiagnosticSeverity {
        /// <summary>An observation that need not lose metadata.</summary>
        Warning,
        /// <summary>An error or limit that prevents complete reading.</summary>
        Error,
    }
}
