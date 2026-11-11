using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Fonts.Metadata.Internal {
    internal ref struct SfntParser {
        // SFNT table-directory layout and tags:
        // https://learn.microsoft.com/en-us/typography/opentype/spec/otff#table-directory
        const uint TrueTypeFlavor = 0x00010000;
        // Four-byte text tags are stored big-endian; UTF-8 literals preserve their wire bytes.
        // WOFF signature: https://www.w3.org/TR/WOFF/#WOFFHeader
        // WOFF2 signature: https://www.w3.org/TR/WOFF2/#woff20Header
        static readonly uint OpenTypeCffFlavor = BinaryPrimitives.ReadUInt32BigEndian("OTTO"u8);
        static readonly uint AppleTrueTypeFlavor = BinaryPrimitives.ReadUInt32BigEndian("true"u8);
        static readonly uint Type1Flavor = BinaryPrimitives.ReadUInt32BigEndian("typ1"u8);
        static readonly uint CollectionSignature = BinaryPrimitives.ReadUInt32BigEndian("ttcf"u8);
        static readonly uint WoffSignature = BinaryPrimitives.ReadUInt32BigEndian("wOFF"u8);
        static readonly uint Woff2Signature = BinaryPrimitives.ReadUInt32BigEndian("wOF2"u8);
        static readonly uint NameTag = BinaryPrimitives.ReadUInt32BigEndian("name"u8);
        static readonly uint Os2Tag = BinaryPrimitives.ReadUInt32BigEndian("OS/2"u8);
        const int TagSize = sizeof(uint);
        const int TableAlignment = 4;
        const int SfntHeaderSize = 12;
        const int TableCountOffset = 4;
        const int TableRecordSize = 16;
        const int TableDataOffsetField = 8;
        const int TableLengthField = 12;

        const uint CollectionVersion1 = 0x00010000;
        const uint CollectionVersion2 = 0x00020000;
        const int CollectionHeaderSize = 12;
        const int CollectionVersionOffset = 4;
        const int CollectionFaceCountOffset = 8;
        const int CollectionDsigDescriptorSize = 12;

        // EOT is little-endian; recognition only needs the prefix through MagicNumber.
        // https://www.w3.org/submissions/EOT/#FileFormat
        const int EotRecognitionPrefixSize = 36;
        const int EotVersionOffset = 8;
        const int EotMagicOffset = 34;
        const ushort EotMagicNumber = 0x504c;
        const uint EotVersion1 = 0x00010000;
        const uint EotVersion2 = 0x00020001;
        const uint EotVersion2WithRootStringChecksum = 0x00020002;

        readonly ReadOnlySpan<byte> data;
        readonly ReadingContext context;
        readonly List<FontMetadata.Face> faces = new();
        readonly List<(long Start, long End)> directories = new();
        readonly List<(long Start, long End)> protectedRanges = new();
        bool hasUsableMetadata, incompleteSections;
        internal SfntParser(ReadOnlySpan<byte> data, FontReadLimits limits, CancellationToken token) {
            this.data = data;
            context = new ReadingContext(limits, token);
        }
        static bool IsSupportedSfntFlavor(uint flavor) {
            return flavor == TrueTypeFlavor || flavor == OpenTypeCffFlavor ||
                flavor == AppleTrueTypeFlavor || flavor == Type1Flavor;
        }
        internal static FontFormat Recognize(ReadOnlySpan<byte> data) {
            if(data.Length >= TagSize) {
                uint signature = MetadataTables.U32(data, 0);
                if(IsSupportedSfntFlavor(signature))
                    return FontFormat.Sfnt;
                if(signature == CollectionSignature)
                    return FontFormat.Collection;
                if(signature == WoffSignature)
                    return FontFormat.Woff;
                if(signature == Woff2Signature)
                    return FontFormat.Woff2;
            }
            return HasEotHeader(data) ? FontFormat.Eot : FontFormat.Unknown;
        }
        static bool HasEotHeader(ReadOnlySpan<byte> data) {
            if(data.Length < EotRecognitionPrefixSize)
                return false;
            ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(EotMagicOffset, sizeof(ushort)));
            if(magic != EotMagicNumber)
                return false;
            uint version = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(EotVersionOffset, sizeof(uint)));
            return version is EotVersion1 or EotVersion2 or EotVersion2WithRootStringChecksum;
        }
        internal FontInspectionResult Parse() {
            context.Token.ThrowIfCancellationRequested();
            var format = Recognize(data);
            if(format == FontFormat.Unknown)
                return new FontInspectionResult(FontReadStatus.NotRecognized, null, context.Diagnostics.Freeze());
            if(format == FontFormat.Sfnt) {
                ProtectDirectory(0);
                MergeProtection();
                faces.Add(ReadFace(0, 0, 0));
            }
            else if(format == FontFormat.Collection) {
                ReadCollection();
            }
            else {
                Error(FontDiagnosticCodes.UnsupportedFormat, Messages.UnsupportedContainer, 0,
                    category: FontDiagnosticCategory.UnsupportedFeature);
            }
            context.Token.ThrowIfCancellationRequested();
            var status = GetReadStatus();
            var faceSnapshot = Array.AsReadOnly(faces.ToArray());
            var metadata = new FontMetadata(format, faceSnapshot);
            var diagnostics = context.Diagnostics.Freeze();
            return new FontInspectionResult(status, metadata, diagnostics);
        }
        FontReadStatus GetReadStatus() {
            bool incomplete = incompleteSections || context.Diagnostics.HasErrors || context.Diagnostics.Truncated;
            if(!incomplete)
                return FontReadStatus.Complete;
            if(hasUsableMetadata)
                return FontReadStatus.Partial;
            if(context.Diagnostics.HasDataFailure || context.Diagnostics.Truncated)
                return FontReadStatus.Failed;
            return FontReadStatus.Unsupported;
        }
        internal static FontInspectionResult InputLimit(ReadOnlySpan<byte> prefix) {
            var format = Recognize(prefix);
            var diagnostic = new FontDiagnostic(FontDiagnosticCodes.LimitExceeded,
                FontDiagnosticSeverity.Error, FontDiagnosticCategory.LimitExceeded, Messages.InputLimit);
            var metadata = format == FontFormat.Unknown ? null : new FontMetadata(format);
            var diagnostics = Array.AsReadOnly([diagnostic]);
            return new FontInspectionResult(FontReadStatus.Failed, metadata, diagnostics);
        }
        void Error(string code, string message, long? offset, int? index = null, string? tag = null,
            FontDiagnosticCategory category = FontDiagnosticCategory.MalformedData) {
            var diagnostic = new FontDiagnostic(code,
                FontDiagnosticSeverity.Error, category, message, index, tag, offset);
            context.Diagnostics.Add(diagnostic);
        }
        void Warning(string message, long offset, int index, string? tag = null) {
            var diagnostic = new FontDiagnostic(FontDiagnosticCodes.NonConformantDirectory,
                FontDiagnosticSeverity.Warning, FontDiagnosticCategory.NonConformantData, message, index, tag, offset);
            context.Diagnostics.Add(diagnostic);
        }
        bool InBounds(long offset, long length) {
            // Subtract only after validating offset; offset + length could overflow for arbitrary ranges.
            return offset >= 0 && length >= 0 && offset <= data.Length && length <= data.Length - offset;
        }
        void ProtectDirectory(long offset) {
            if(!InBounds(offset, SfntHeaderSize))
                return;
            // An unknown flavor cannot establish an SFNT directory layout. Trusting its count
            // could reserve unrelated metadata and prevent recovery of a valid sibling face.
            if(!IsSupportedSfntFlavor(MetadataTables.U32(data, (int)offset)))
                return;
            int count = MetadataTables.U16(data, (int)offset + TableCountOffset);
            long length = GetDirectoryLength(count);
            if(count > 0 && InBounds(offset, length))
                directories.Add((offset, offset + length));
        }
        static long GetDirectoryLength(int count) {
            return SfntHeaderSize + (long)count * TableRecordSize;
        }
        void MergeProtection() {
            // Merge half-open header ranges so table lookups can use binary search.
            // Keep the original ranges separately to detect overlapping face directories.
            directories.Sort((left, right) => left.Start.CompareTo(right.Start));
            foreach(var range in directories) {
                if(protectedRanges.Count > 0 && range.Start <= protectedRanges[^1].End) {
                    var last = protectedRanges[^1];
                    protectedRanges[^1] = (last.Start, Math.Max(last.End, range.End));
                }
                else protectedRanges.Add(range);
            }
        }
        bool OverlapsDirectory(long start, long length) {
            // Find the first protected range ending after the table starts.
            int low = 0, high = protectedRanges.Count;
            while(low < high) {
                int middle = low + (high - low) / 2;
                if(protectedRanges[middle].End <= start)
                    low = middle + 1;
                else high = middle;
            }
            return low < protectedRanges.Count && protectedRanges[low].Start < start + length;
        }
        void ReadCollection() {
            // TTC offsets, including table offsets within each face, are absolute from the collection origin.
            // v2 appends a 12-byte DSIG descriptor after the face offset array.
            // https://learn.microsoft.com/en-us/typography/opentype/spec/otff#ttc-header
            if(data.Length < CollectionHeaderSize) {
                Error(FontDiagnosticCodes.InvalidCollection, Messages.TtcHeaderTruncated, 0);
                return;
            }
            uint version = MetadataTables.U32(data, CollectionVersionOffset);
            uint count = MetadataTables.U32(data, CollectionFaceCountOffset);
            if(version is not CollectionVersion1 and not CollectionVersion2) {
                Error(FontDiagnosticCodes.UnsupportedTableVersion, Messages.UnsupportedCollectionVersion, CollectionVersionOffset,
                    category: FontDiagnosticCategory.UnsupportedFeature);
                return;
            }
            long headerEnd = GetCollectionHeaderLength(version, count);
            if(count == 0 || headerEnd > data.Length) {
                Error(FontDiagnosticCodes.InvalidCollection, Messages.InvalidCollectionOffsets, CollectionFaceCountOffset);
                return;
            }
            directories.Add((0, headerEnd));
            int allowed = (int)Math.Min(count, (uint)context.Limits.MaxFaces);
            for(int i = 0; i < allowed; i++) {
                context.Token.ThrowIfCancellationRequested();
                long offset = ReadCollectionFaceOffset(i);
                if(offset >= headerEnd)
                    ProtectDirectory(offset);
            }
            MergeProtection();
            for(int i = 0; i < allowed; i++) {
                context.Token.ThrowIfCancellationRequested();
                long offset = ReadCollectionFaceOffset(i);
                faces.Add(ReadFace(i, offset, headerEnd));
            }
            if(count > allowed) {
                Error(FontDiagnosticCodes.LimitExceeded, Messages.FaceLimit, CollectionFaceCountOffset,
                    category: FontDiagnosticCategory.LimitExceeded);
            }
        }
        static long GetCollectionHeaderLength(uint version, uint count) {
            // Widen before multiplication: the untrusted face count occupies all 32 bits.
            long length = CollectionHeaderSize + (long)count * sizeof(uint);
            return version == CollectionVersion2 ? length + CollectionDsigDescriptorSize : length;
        }
        uint ReadCollectionFaceOffset(int index) {
            return MetadataTables.U32(data, CollectionHeaderSize + index * sizeof(uint));
        }
        FontMetadata.Face EmptyFace(int index, uint? flavor = null) {
            return new FontMetadata.Face(index, flavor);
        }
        FontMetadata.Face ReadFace(int index, long offset, long headerEnd) {
            if(offset < headerEnd || !InBounds(offset, SfntHeaderSize)) {
                Error(FontDiagnosticCodes.InvalidDirectory, Messages.InvalidFaceHeader, offset, index);
                return EmptyFace(index);
            }
            int start = (int)offset;
            uint flavor = MetadataTables.U32(data, start);
            if(!IsSupportedSfntFlavor(flavor)) {
                Error(FontDiagnosticCodes.UnsupportedFormat, Messages.UnsupportedSfntFlavor, offset, index,
                    category: FontDiagnosticCategory.UnsupportedFeature);
                return EmptyFace(index, flavor);
            }
            int count = MetadataTables.U16(data, start + TableCountOffset);
            long end = offset + GetDirectoryLength(count);
            if(count == 0 || end > data.Length) {
                Error(FontDiagnosticCodes.InvalidDirectory, Messages.InvalidTableDirectory, offset + TableCountOffset, index);
                return EmptyFace(index, flavor);
            }
            if(count > context.Limits.MaxTablesPerFace) {
                Error(FontDiagnosticCodes.LimitExceeded, Messages.TableCountLimit, offset + TableCountOffset, index,
                    category: FontDiagnosticCategory.LimitExceeded);
                return EmptyFace(index, flavor);
            }
            if(OverlapsAnotherDirectory(offset, end)) {
                Error(FontDiagnosticCodes.InvalidDirectory, Messages.OverlappingDirectories, offset, index);
                return EmptyFace(index, flavor);
            }
            if(offset % TableAlignment != 0)
                Warning(Messages.UnalignedDirectory, offset, index);
            var tables = new Dictionary<uint, TableRange>();
            var ranges = new List<TableRange>();
            bool directoryError = false, orderReported = false;
            uint previous = 0;
            for(int i = 0; i < count; i++) {
                context.Token.ThrowIfCancellationRequested();
                int pos = start + SfntHeaderSize + i * TableRecordSize;
                uint tag = MetadataTables.U32(data, pos);
                var entry = new TableRange(tag,
                    MetadataTables.U32(data, pos + TableDataOffsetField),
                    MetadataTables.U32(data, pos + TableLengthField));
                if(i > 0 && tag < previous && !orderReported) {
                    Warning(Messages.UnsortedTableTags, pos, index);
                    orderReported = true;
                }
                previous = tag;
                if(tables.TryGetValue(tag, out var duplicate)) {
                    duplicate.Readable = entry.Readable = false;
                    directoryError = true;
                    Error(FontDiagnosticCodes.InvalidDirectory, Messages.DuplicateTableTags, pos, index, entry.TagText);
                }
                else
                    tables.Add(tag, entry);
                if(!InBounds(entry.Offset, entry.Length)) {
                    entry.Readable = false;
                    directoryError = true;
                    Error(FontDiagnosticCodes.InvalidDirectory, Messages.TableOutOfBounds, pos + TableDataOffsetField, index, entry.TagText);
                    continue;
                }
                if(entry.Length == 0)
                    continue;
                if(OverlapsDirectory(entry.Offset, entry.Length)) {
                    entry.Readable = false;
                    directoryError = true;
                    Error(FontDiagnosticCodes.InvalidDirectory, Messages.TableOverlapsDirectory, pos + TableDataOffsetField, index, entry.TagText);
                }
                if(entry.Offset % TableAlignment != 0)
                    Warning(Messages.UnalignedTable, pos + TableDataOffsetField, index, entry.TagText);
                ranges.Add(entry);
            }
            directoryError |= InvalidateOverlappingTables(ranges, index);
            TableMetadata? names = ReadTable(tables.GetValueOrDefault(NameTag), index);
            TableMetadata? os2 = ReadTable(tables.GetValueOrDefault(Os2Tag), index);
            incompleteSections |= !IsCompleteSection(names.Status) || !IsCompleteSection(os2.Status);
            hasUsableMetadata |= HasUsableFaceMetadata(names.Status, os2.Status, directoryError);
            return new FontMetadata.Face(index, flavor, names.Status, os2.Status, names.Records, os2.VendorId, os2.Permissions, names.PreferredNames);
        }
        bool OverlapsAnotherDirectory(long start, long end) {
            foreach(var range in directories) {
                context.Token.ThrowIfCancellationRequested();
                if(range.Start != start && start < range.End && end > range.Start)
                    return true;
            }
            return false;
        }
        bool InvalidateOverlappingTables(List<TableRange> ranges, int index) {
            bool overlaps = false;
            ranges.Sort((left, right) => left.Offset.CompareTo(right.Offset));
            // Reject whole overlapping groups, not just adjacent pairs: a long range can
            // contain several shorter ranges. Half-open ranges allow adjacent tables.
            // SFNT tables are separate byte ranges; TTC sharing is handled across faces:
            // https://learn.microsoft.com/en-us/typography/opentype/spec/otff#font-collections
            for(int begin = 0; begin < ranges.Count;) {
                context.Token.ThrowIfCancellationRequested();
                int next = begin + 1;
                long groupEnd = ranges[begin].Offset + ranges[begin].Length;
                while(next < ranges.Count && ranges[next].Offset < groupEnd) {
                    groupEnd = Math.Max(groupEnd, ranges[next].Offset + ranges[next].Length);
                    next++;
                }
                if(next > begin + 1) {
                    overlaps = true;
                    for(int i = begin; i < next; i++) {
                        ranges[i].Readable = false;
                        Error(FontDiagnosticCodes.InvalidDirectory, Messages.OverlappingTables,
                            ranges[i].Offset, index, ranges[i].TagText);
                    }
                }
                begin = next;
            }
            return overlaps;
        }
        static bool IsCompleteSection(FontSectionStatus status) {
            return status is FontSectionStatus.Complete or FontSectionStatus.Missing;
        }
        static bool HasUsableSectionMetadata(FontSectionStatus status) {
            return status is FontSectionStatus.Complete or FontSectionStatus.Partial;
        }
        static bool HasUsableFaceMetadata(FontSectionStatus names, FontSectionStatus os2, bool directoryError) {
            if(HasUsableSectionMetadata(names) || HasUsableSectionMetadata(os2))
                return true;
            // A valid directory proving both tables absent is useful metadata too.
            return !directoryError && names == FontSectionStatus.Missing && os2 == FontSectionStatus.Missing;
        }
        TableMetadata ReadTable(TableRange? range, int index) {
            if(range == null)
                return new TableMetadata { Status = FontSectionStatus.Missing };
            if(!range.Readable)
                return new TableMetadata { Status = FontSectionStatus.Invalid };
            if(range.Length > context.Limits.MaxTableBytes) {
                Error(FontDiagnosticCodes.LimitExceeded, Messages.TableSizeLimit, range.Offset, index, range.TagText,
                    FontDiagnosticCategory.LimitExceeded);
                return new TableMetadata { Status = FontSectionStatus.NotRead };
            }
            // Readable ranges have already been bounded by the input span, so these casts are safe.
            // Cache source metadata and preferred names once, then localize diagnostics per face.
            var key = (range.Tag, (int)range.Offset, (int)range.Length);
            if(!context.Tables.TryGetValue(key, out var result)) {
                var table = data.Slice(key.Item2, key.Item3);
                result = range.Tag == NameTag
                    ? MetadataTables.ReadNames(table, key.Item2, context)
                    : MetadataTables.ReadOs2(table, key.Item2, context);
                context.Tables.Add(key, result);
            }
            foreach(var diagnostic in result.Diagnostics)
                context.Diagnostics.Add(diagnostic.ForFace(index));
            return result;
        }
        static class Messages {
            internal const string UnsupportedContainer = "This container is recognized, but its reader is not implemented.";
            internal const string InputLimit = "MaxInputBytes prevented parsing the input.";
            internal const string TtcHeaderTruncated = "The collection header is truncated.";
            internal const string UnsupportedCollectionVersion = "The collection version is not supported.";
            internal const string InvalidCollectionOffsets = "The collection face offset array is empty or truncated.";
            internal const string FaceLimit = "MaxFaces prevented reading all collection faces.";
            internal const string InvalidFaceHeader = "The face header is outside the input or inside the collection header.";
            internal const string UnsupportedSfntFlavor = "The face SFNT flavor is not supported.";
            internal const string InvalidTableDirectory = "The SFNT table directory is empty or truncated.";
            internal const string TableCountLimit = "MaxTablesPerFace prevented reading the directory.";
            internal const string OverlappingDirectories = "Face directories overlap.";
            internal const string UnalignedDirectory = "The SFNT directory is not four-byte aligned.";
            internal const string UnsortedTableTags = "Table tags are not sorted; unique entries are still readable.";
            internal const string DuplicateTableTags = "Duplicate table tags are ambiguous.";
            internal const string TableOutOfBounds = "The table range extends outside the input.";
            internal const string TableOverlapsDirectory = "The table overlaps a container or face directory.";
            internal const string UnalignedTable = "The table offset is not four-byte aligned.";
            internal const string OverlappingTables = "Table ranges overlap within this face.";
            internal const string TableSizeLimit = "MaxTableBytes prevented reading the metadata table.";
        }
        sealed class TableRange {
            internal TableRange(uint tag, long offset, long length) {
                Tag = tag;
                Offset = offset;
                Length = length;
            }
            internal uint Tag { get; }
            internal long Offset { get; }
            internal long Length { get; }
            internal bool Readable { get; set; } = true;
            internal string TagText {
                get {
                    Span<byte> bytes = stackalloc byte[TagSize];
                    BinaryPrimitives.WriteUInt32BigEndian(bytes, Tag);
                    return Encoding.Latin1.GetString(bytes);
                }
            }
        }
    }
}
