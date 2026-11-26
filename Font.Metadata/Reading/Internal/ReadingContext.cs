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
        internal int Count { get { return items.Count; } }
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
                if(limit > 0)
                    MarkLastSuppressed();
            }
        }
        void MarkLastSuppressed() {
            items[^1] = new FontDiagnostic(FontDiagnosticCodes.DiagnosticsTruncated,
                FontDiagnosticSeverity.Warning, FontDiagnosticCategory.LimitExceeded, DiagnosticsSuppressedMessage);
        }
        internal ReadOnlyCollection<FontDiagnostic> SectionView() {
            return items.Count == 0 ? ReadOnlyCollection<FontDiagnostic>.Empty : new ResultList<FontDiagnostic>(items);
        }
        internal void TrimSection(int keep, bool markLast) {
            items.RemoveRange(keep, Count - keep);
            if(markLast)
                MarkLastSuppressed();
        }
        internal IReadOnlyList<FontDiagnostic> Freeze() {
            return ResultList<FontDiagnostic>.Snapshot(items);
        }
    }
    internal sealed partial class ReadingContext {
        internal ReadingContext(FontReadLimits limits, CancellationToken token, Action? checkCancellation = null) {
            Limits = limits;
            Token = token;
            CheckCancellation = checkCancellation ?? token.ThrowIfCancellationRequested;
            Diagnostics = new DiagnosticBag(limits.MaxDiagnostics);
        }
        List<DiagnosticBag>? sectionDiagnostics;
        int retainedSectionDiagnostics;
        bool sectionDiagnosticsOmitted;
        internal DiagnosticBag NewSectionDiagnostics() {
            return new DiagnosticBag(Limits.MaxDiagnostics - retainedSectionDiagnostics);
        }
        internal ReadOnlyCollection<FontDiagnostic> RetainSectionDiagnostics(DiagnosticBag bag) {
            sectionDiagnosticsOmitted |= bag.Truncated;
            if(bag.Count > 0) {
                sectionDiagnostics ??= new List<DiagnosticBag>();
                sectionDiagnostics.Add(bag);
                retainedSectionDiagnostics += bag.Count;
            }
            return bag.SectionView();
        }
        internal void FinishSectionDiagnostics() {
            // Container and baseline-table errors may arrive after an optional section was parsed.
            // Finalize the read-only views before publishing faces. Shared tables own one view.
            int remaining = Limits.MaxDiagnostics - Diagnostics.Count;
            if(sectionDiagnostics == null || (!sectionDiagnosticsOmitted && retainedSectionDiagnostics <= remaining))
                return;
            foreach(var bag in sectionDiagnostics) {
                int keep = Math.Min(bag.Count, remaining);
                remaining -= keep;
                bag.TrimSection(keep, keep > 0 && remaining == 0);
            }
        }
        internal bool IncludeTableInventory { get; init; }
        internal FontTableInventory? EmptyInventory(FontSectionStatus status = FontSectionStatus.NotRead) {
            return IncludeTableInventory ? new FontTableInventory(status) : null;
        }
        internal bool IncludeUnicodeCoverage { get; init; }
        internal bool IncludeVariationAxes { get; init; }
        internal bool WantsOptionalTable(uint tag) {
            return (IncludeUnicodeCoverage && tag == ContainerLayout.CmapTag) ||
                (IncludeVariationAxes && tag == ContainerLayout.FvarTag);
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
                throw new DecodeLimitException(nameof(FontReadLimits.MaxRetainedDecodedBytes));
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
            FontDiagnosticCategory category = FontDiagnosticCategory.MalformedData, string? limitReason = null) {
            var diagnostic = new FontDiagnostic(code, FontDiagnosticSeverity.Error, category, message, face, tag, offset, limitReason: limitReason);
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
            TableMetadata result;
            if(tag == ContainerLayout.CmapTag) {
                var coverage = CmapParser.Read(data, location, this);
                result = new TableMetadata { Status = coverage.Status, Coverage = coverage };
            }
            else if(tag == ContainerLayout.FvarTag) {
                var axes = FvarParser.Read(data, location, this);
                result = new TableMetadata { Status = axes.Status, Axes = axes };
            }
            else if(tag == ContainerLayout.HeadTag || tag == ContainerLayout.MaxpTag)
                result = HeaderTables.Read(tag, data, location);
            else if(tag == ContainerLayout.NameTag)
                result = MetadataTables.ReadNames(data, location, this);
            else
                result = MetadataTables.ReadOs2(data, location, this);
            if(!complete)
                result = result.WithIncompleteSource();
            Tables.Add(key, result);
            return result;
        }
        internal FontMetadata.Face CreateFace(int index, uint? flavor, TableMetadata names, TableMetadata os2,
            bool directoryValid = true, FontFaceSource source = FontFaceSource.Container,
            TableMetadata? cmap = null, TableMetadata? fvar = null, TableMetadata? head = null,
            TableMetadata? maxp = null, FontTableInventory? inventory = null) {
            foreach(var diagnostic in names.Diagnostics)
                Diagnostics.Add(diagnostic.ForFace(index));
            foreach(var diagnostic in os2.Diagnostics)
                Diagnostics.Add(diagnostic.ForFace(index));
            IncompleteSections |= !IsComplete(names.Status) || !IsComplete(os2.Status);
            // Preserve the 1.0 classification of failures in name, OS/2 and the directory.
            // When both older tables are absent, the new sections distinguish an empty face
            // from a face whose only declared metadata failed.
            bool legacyTablesMissing = names.Status == FontSectionStatus.Missing && os2.Status == FontSectionStatus.Missing;
            bool headersMissing = head?.Status == FontSectionStatus.Missing && maxp?.Status == FontSectionStatus.Missing;
            bool headerReadable = IsUsable(head?.Status ?? FontSectionStatus.NotRead) || IsUsable(maxp?.Status ?? FontSectionStatus.NotRead);
            HasUsableMetadata |= IsUsable(names.Status) || IsUsable(os2.Status) ||
                (directoryValid && legacyTablesMissing && (headersMissing || headerReadable));
            AddHeaderStatus(head, index);
            AddHeaderStatus(maxp, index);
            return new FontMetadata.Face(index, flavor, names.Status, os2.Status, names.Records,
                os2.VendorId, os2.Permissions, names.PreferredNames, source, GetCoverage(cmap), GetAxes(fvar),
                head?.Head, maxp?.GlyphCount, head?.Status ?? names.Status, maxp?.Status ?? os2.Status, inventory ?? EmptyInventory(names.Status));
        }
        void AddHeaderStatus(TableMetadata? table, int index) {
            if(table == null)
                return;
            foreach(var diagnostic in table.Diagnostics)
                Diagnostics.Add(diagnostic.ForFace(index));
            IncompleteSections |= !IsComplete(table.Status);
        }
        internal TableMetadata SharedTableFailure(SourceLocation source, long offset, long length, uint tag,
            int faceIndex, FontSectionStatus status, FontDiagnostic diagnostic) {
            if(!WantsOptionalTable(tag))
                return TableFailure(tag, status, diagnostic.ForFace(faceIndex));
            var key = (source.Id, tag, offset, length);
            if(!Tables.TryGetValue(key, out var result)) {
                result = TableFailure(tag, status, diagnostic);
                Tables.Add(key, result);
            }
            return result;
        }
        internal TableMetadata TableFailure(uint tag, FontSectionStatus status, FontDiagnostic diagnostic) {
            if(!WantsOptionalTable(tag)) {
                Diagnostics.Add(diagnostic);
                return TableMetadata.Empty(status);
            }
            var bag = NewSectionDiagnostics();
            bag.Add(diagnostic);
            var messages = RetainSectionDiagnostics(bag);
            if(tag == ContainerLayout.CmapTag)
                return new TableMetadata { Status = status, Coverage = new FontUnicodeCoverage(status, messages) };
            return new TableMetadata { Status = status, Axes = new FontVariationAxes(status, hasTableEntry: true, diagnostics: messages) };
        }
        internal FontUnicodeCoverage? GetCoverage(TableMetadata? table) {
            if(!IncludeUnicodeCoverage)
                return null;
            return table?.Coverage ?? new FontUnicodeCoverage(table?.Status ?? FontSectionStatus.NotRead);
        }
        internal FontVariationAxes? GetAxes(TableMetadata? table) {
            if(!IncludeVariationAxes)
                return null;
            return table?.Axes ?? new FontVariationAxes(table?.Status ?? FontSectionStatus.NotRead, hasTableEntry: false);
        }
        internal static bool IsComplete(FontSectionStatus status) {
            return status is FontSectionStatus.Complete or FontSectionStatus.Missing;
        }
        internal static bool IsUsable(FontSectionStatus status) {
            return status is FontSectionStatus.Complete or FontSectionStatus.Partial;
        }
        internal Dictionary<(int Source, uint Tag, long Offset, long Length), TableMetadata> Tables { get; } = new();
    }
    internal sealed class TableMetadata {
        internal static TableMetadata Empty(FontSectionStatus status) {
            return new TableMetadata { Status = status };
        }
        internal static TableMetadata UnreadableEntry(uint tag, FontSectionStatus status) {
            if(tag == ContainerLayout.FvarTag)
                return new TableMetadata { Status = status, Axes = new FontVariationAxes(status, hasTableEntry: true) };
            return Empty(status);
        }
        internal TableMetadata WithIncompleteSource() {
            if(Status != FontSectionStatus.Complete)
                return this;
            return new TableMetadata {
                Status = FontSectionStatus.Partial,
                Coverage = Coverage?.WithStatus(FontSectionStatus.Partial),
                Axes = Axes?.WithStatus(FontSectionStatus.Partial),
                Head = Head,
                GlyphCount = GlyphCount,
                Records = Records,
                PreferredNames = PreferredNames,
                VendorId = VendorId,
                Permissions = Permissions,
                Diagnostics = Diagnostics
            };
        }
        internal FontUnicodeCoverage? Coverage { get; init; }
        internal FontVariationAxes? Axes { get; init; }
        internal FontSectionStatus Status { get; init; }
        internal IReadOnlyList<FontMetadata.NameRecord> Records { get; init; } = ReadOnlyCollection<FontMetadata.NameRecord>.Empty;
        internal IReadOnlyDictionary<int, string> PreferredNames { get; init; } = ReadOnlyDictionary<int, string>.Empty;
        internal FontHeader? Head { get; init; }
        internal ushort? GlyphCount { get; init; }
        internal string? VendorId { get; init; }
        internal ushort? Permissions { get; init; }
        internal IReadOnlyList<FontDiagnostic> Diagnostics { get; init; } = ReadOnlyCollection<FontDiagnostic>.Empty;
    }
}
