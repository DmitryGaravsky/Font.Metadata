using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System;

namespace Fonts.Metadata.Internal {
    internal sealed class DiagnosticBag {
        const string DiagnosticsSuppressedMessage = "Additional diagnostics were suppressed by MaxDiagnostics.";
        readonly int limit;
        readonly List<FontDiagnostic> items = new List<FontDiagnostic>();
        internal DiagnosticBag(int limit) {
            this.limit = limit;
        }
        internal bool HasErrors { get; private set; }
        internal bool HasDataFailure { get; private set; }
        internal bool Truncated { get; private set; }
        internal void Add(FontDiagnostic item) {
            HasErrors |= item.Severity == FontDiagnosticSeverity.Error;
            HasDataFailure |= item.Category is FontDiagnosticCategory.MalformedData or FontDiagnosticCategory.LimitExceeded;
            if(items.Count < limit) {
                items.Add(item);
            }
            else if(!Truncated) {
                Truncated = true;
                items[limit - 1] = new FontDiagnostic(FontDiagnosticCodes.DiagnosticsTruncated,
                    FontDiagnosticSeverity.Warning, FontDiagnosticCategory.LimitExceeded,
                    DiagnosticsSuppressedMessage);
            }
        }
        internal IReadOnlyList<FontDiagnostic> Freeze() {
            return Array.AsReadOnly(items.ToArray());
        }
    }
    internal sealed class ReadingContext {
        internal ReadingContext(FontReadLimits limits, CancellationToken token) {
            Limits = limits;
            Token = token;
            Diagnostics = new DiagnosticBag(limits.MaxDiagnostics);
        }
        internal FontReadLimits Limits { get; }
        internal CancellationToken Token { get; }
        internal DiagnosticBag Diagnostics { get; }
        internal long NameStringBytes { get; set; }
        internal Dictionary<(uint Tag, int Offset, int Length), TableMetadata> Tables { get; } = new();
    }
    internal sealed class TableMetadata {
        internal FontSectionStatus Status { get; init; }
        internal IReadOnlyList<FontMetadata.NameRecord> Records { get; init; } = ReadOnlyCollection<FontMetadata.NameRecord>.Empty;
        internal IReadOnlyDictionary<int, string> PreferredNames { get; init; } = ReadOnlyDictionary<int, string>.Empty;
        internal string? VendorId { get; init; }
        internal ushort? Permissions { get; init; }
        internal IReadOnlyList<FontDiagnostic> Diagnostics { get; init; } = ReadOnlyCollection<FontDiagnostic>.Empty;
    }
}
