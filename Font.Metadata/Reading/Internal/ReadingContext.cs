using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

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
            if(items.Count == 0)
                return ReadOnlyCollection<FontDiagnostic>.Empty;
            var snapshot = items.ToArray();
            return Array.AsReadOnly(snapshot);
        }
    }
    internal sealed class ReadingContext {
        internal ReadingContext(FontReadLimits limits, CancellationToken token, Action? checkCancellation = null) {
            Limits = limits;
            Token = token;
            CheckCancellation = checkCancellation ?? token.ThrowIfCancellationRequested;
            Diagnostics = new DiagnosticBag(limits.MaxDiagnostics);
        }
        internal FontReadLimits Limits { get; }
        internal CancellationToken Token { get; }
        internal Action CheckCancellation { get; }
        internal DiagnosticBag Diagnostics { get; }
        internal long NameStringBytes { get; set; }
        internal const int CopyChunkSize = 8192;
        int sourceId;
        internal long DecodedBytes { get; set; }
        internal long RetainedBytes { get; private set; }
        internal bool HasUsableMetadata { get; set; }
        internal bool IncompleteSections { get; set; }
        internal List<FontMetadata.Face> Faces { get; } = new();
        internal FontMetadata.EotHeader? Eot { get; set; }
        internal FontSectionStatus EotStatus { get; set; } = FontSectionStatus.NotRead;
        internal SourceLocation NewSource(long? offset, bool compressed = false) {
            return new SourceLocation(++sourceId, offset, compressed);
        }
        internal void ReserveRetained(int length) {
            CheckCancellation();
            if(length < 0 || length > Array.MaxLength || length > Limits.MaxRetainedDecodedBytes - RetainedBytes)
                throw new DecodeLimitException();
            RetainedBytes += length;
        }
        internal void ReleaseRetained(int length) {
            RetainedBytes -= length;
        }
        internal FontReadStatus GetStatus() {
            bool incomplete = IncompleteSections || Diagnostics.HasErrors || Diagnostics.Truncated;
            if(!incomplete)
                return FontReadStatus.Complete;
            if(HasUsableMetadata)
                return FontReadStatus.Partial;
            return Diagnostics.HasDataFailure || Diagnostics.Truncated ? FontReadStatus.Failed : FontReadStatus.Unsupported;
        }
        internal void Error(string code, string message, long? offset = null, int? face = null, string? tag = null,
            FontDiagnosticCategory category = FontDiagnosticCategory.MalformedData) {
            var diagnostic = new FontDiagnostic(code, FontDiagnosticSeverity.Error, category, message, face, tag, offset);
            Diagnostics.Add(diagnostic);
        }
        internal void Warning(string message, long? offset = null) {
            var diagnostic = new FontDiagnostic(FontDiagnosticCodes.NonConformantContainer,
                FontDiagnosticSeverity.Warning, FontDiagnosticCategory.NonConformantData, message, byteOffset: offset);
            Diagnostics.Add(diagnostic);
        }
        internal TableMetadata ReadTable(uint tag, ReadOnlySpan<byte> data, SourceLocation source, long offset, bool complete = true) {
            var key = (source.Id, tag, offset, data.Length);
            if(Tables.TryGetValue(key, out var cached))
                return cached;
            var location = source.At(offset);
            var result = tag == ContainerLayout.NameTag
                ? MetadataTables.ReadNames(data, location, this)
                : MetadataTables.ReadOs2(data, location, this);
            if(!complete)
                result = result.WithIncompleteSource();
            Tables.Add(key, result);
            return result;
        }
        internal FontMetadata.Face CreateFace(int index, uint? flavor, TableMetadata names, TableMetadata os2,
            bool directoryValid = true, FontFaceSource source = FontFaceSource.Container) {
            foreach(var diagnostic in names.Diagnostics)
                Diagnostics.Add(diagnostic.ForFace(index));
            foreach(var diagnostic in os2.Diagnostics)
                Diagnostics.Add(diagnostic.ForFace(index));
            IncompleteSections |= !IsComplete(names.Status) || !IsComplete(os2.Status);
            HasUsableMetadata |= IsUsable(names.Status) || IsUsable(os2.Status) ||
                (directoryValid && names.Status == FontSectionStatus.Missing && os2.Status == FontSectionStatus.Missing);
            return new FontMetadata.Face(index, flavor, names.Status, os2.Status, names.Records,
                os2.VendorId, os2.Permissions, names.PreferredNames, source);
        }
        internal static bool IsComplete(FontSectionStatus status) {
            return status is FontSectionStatus.Complete or FontSectionStatus.Missing;
        }
        internal static bool IsUsable(FontSectionStatus status) {
            return status is FontSectionStatus.Complete or FontSectionStatus.Partial;
        }

        internal Dictionary<(int Source, uint Tag, long Offset, int Length), TableMetadata> Tables { get; } = new();
    }
    internal sealed class TableMetadata {
        internal static TableMetadata Empty(FontSectionStatus status) {
            return new TableMetadata { Status = status };
        }
        internal TableMetadata WithIncompleteSource() {
            if(Status != FontSectionStatus.Complete)
                return this;
            return new TableMetadata {
                Status = FontSectionStatus.Partial,
                Records = Records,
                PreferredNames = PreferredNames,
                VendorId = VendorId,
                Permissions = Permissions,
                Diagnostics = Diagnostics
            };
        }
        internal FontSectionStatus Status { get; init; }
        internal IReadOnlyList<FontMetadata.NameRecord> Records { get; init; } = ReadOnlyCollection<FontMetadata.NameRecord>.Empty;
        internal IReadOnlyDictionary<int, string> PreferredNames { get; init; } = ReadOnlyDictionary<int, string>.Empty;
        internal string? VendorId { get; init; }
        internal ushort? Permissions { get; init; }
        internal IReadOnlyList<FontDiagnostic> Diagnostics { get; init; } = ReadOnlyCollection<FontDiagnostic>.Empty;
    }
}
