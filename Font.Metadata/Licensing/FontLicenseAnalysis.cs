using System.Collections.Generic;
using Fonts.Metadata.Internal;

namespace Fonts.Metadata {
    /// <summary>Immutable license indications and completeness of the source name table.</summary>
    public sealed class FontLicenseAnalysis {
        internal FontLicenseAnalysis(bool isSourceComplete, List<FontLicenseHint> hints) {
            IsSourceComplete = isSourceComplete;
            Hints = ResultList<FontLicenseHint>.Snapshot(hints);
        }
        /// <summary>True only for a Complete or Missing name table, independently of recognition coverage.</summary>
        public bool IsSourceComplete { get; }
        /// <summary>
        /// Indications in record order, then rule order, then first-evidence order.
        /// Conflicting records remain separate. An empty collection does not prove the absence of a license.
        /// </summary>
        public IReadOnlyList<FontLicenseHint> Hints { get; }
    }
}
