using System.Globalization;
using System.IO;
using System;

namespace Fonts.Metadata {
    /// <summary>A strict read could not return complete metadata. Inspect Result for partial data.</summary>
    public sealed class FontReadException : IOException {
        const string IncompleteReadMessage = "Font metadata reading ended with {0}.";
        internal FontReadException(FontInspectionResult result) : base(string.Format(CultureInfo.InvariantCulture, IncompleteReadMessage, result.Status)) {
            Result = result;
        }
        /// <summary>The same inspection result available from the diagnostic reading API.</summary>
        public FontInspectionResult Result { get; }
    }
    /// <summary>A strict read encountered an unsupported format or feature without usable metadata.</summary>
    public sealed class FontFeatureNotSupportedException : NotSupportedException {
        const string UnsupportedFeatureMessage = "The recognized font format or feature is not supported.";
        internal FontFeatureNotSupportedException(FontInspectionResult result) : base(UnsupportedFeatureMessage) {
            Result = result;
        }
        /// <summary>The inspection result and its diagnostic evidence.</summary>
        public FontInspectionResult Result { get; }
    }
}
