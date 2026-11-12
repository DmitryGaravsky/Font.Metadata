using System;
using System.Collections.Generic;
using System.IO;

namespace Fonts.Metadata.Internal {
    internal static class WoffParser {
        // https://www.w3.org/TR/WOFF/#WOFFHeader and #TableDirectory
        const int HeaderSize = 44, RecordSize = 20;
        const int FlavorOffset = 4, LengthOffset = 8, CountOffset = 12, ReservedOffset = 14, SfntSizeOffset = 16;
        const int MetadataOffset = 24, MetadataLength = 28, MetadataOriginalLength = 32, PrivateOffset = 36, PrivateLength = 40;
        const int TableOffset = 4, CompressedLength = 8, OriginalLength = 12;
        internal static void Read(ReadOnlySpan<byte> data, ReadingContext context) {
            if(data.Length < HeaderSize) {
                context.Error(FontDiagnosticCodes.InvalidContainer, ContainerMessages.Header, 0);
                return;
            }
            uint flavor = MetadataTables.U32(data, FlavorOffset);
            if(!SfntParser.IsSupportedSfntFlavor(flavor)) {
                context.Error(FontDiagnosticCodes.UnsupportedFormat, ContainerMessages.UnsupportedFlavor, FlavorOffset,
                    category: FontDiagnosticCategory.UnsupportedFeature);
                return;
            }
            uint declaredLength = MetadataTables.U32(data, LengthOffset);
            int size = (int)Math.Min(declaredLength, (uint)data.Length);
            int count = MetadataTables.U16(data, CountOffset);
            long directoryEnd = HeaderSize + (long)count * RecordSize;
            bool valid = declaredLength == data.Length && MetadataTables.U16(data, ReservedOffset) == 0;
            if(!valid)
                context.Error(FontDiagnosticCodes.InvalidContainer, ContainerMessages.Header, LengthOffset);
            if(count == 0 || directoryEnd > size) {
                context.Error(FontDiagnosticCodes.InvalidDirectory, ContainerMessages.Header, CountOffset, 0);
                return;
            }
            if(count > context.Limits.MaxTablesPerFace) {
                context.Error(FontDiagnosticCodes.LimitExceeded, ContainerMessages.Limit, CountOffset, 0,
                    category: FontDiagnosticCategory.LimitExceeded);
                return;
            }
            var tables = new Dictionary<uint, Entry>();
            var ranges = new List<Entry>();
            long sfntSize = ContainerLayout.SfntHeaderSize + (long)count * ContainerLayout.SfntRecordSize;
            uint previous = 0;
            for(int i = 0; i < count; i++) {
                context.CheckCancellation();
                int record = HeaderSize + i * RecordSize;
                uint tag = MetadataTables.U32(data, record);
                var entry = new Entry(MetadataTables.U32(data, record + TableOffset),
                    MetadataTables.U32(data, record + CompressedLength), tag) {
                    Original = MetadataTables.U32(data, record + OriginalLength)
                };
                sfntSize += ContainerLayout.Align(entry.Original);
                if(tables.TryGetValue(tag, out var duplicate)) {
                    duplicate.Readable = entry.Readable = false;
                    context.Error(FontDiagnosticCodes.InvalidDirectory, ContainerMessages.Duplicate, record, 0, ContainerLayout.TagText(tag));
                    valid = false;
                }
                else tables.Add(tag, entry);
                if(i > 0 && tag < previous) {
                    context.Error(FontDiagnosticCodes.InvalidDirectory, ContainerMessages.Header, record, 0);
                    valid = false;
                }
                previous = tag;
                if(entry.Length > entry.Original || entry.Start < directoryEnd ||
                    !ContainerLayout.InBounds(entry.Start, entry.Length, size)) {
                    entry.Readable = false;
                    valid = false;
                    context.Error(FontDiagnosticCodes.InvalidDirectory, ContainerMessages.Range, record + TableOffset, 0, ContainerLayout.TagText(tag));
                }
                // WOFF pads every font table, including the final table, to a four-byte boundary.
                if(ContainerLayout.InBounds(entry.Start, entry.Length, size) && ContainerLayout.Align(entry.End) > size) {
                    valid = false;
                    context.Error(FontDiagnosticCodes.InvalidContainer, ContainerMessages.Range, entry.End);
                }
                if(entry.Start % ContainerLayout.Alignment != 0) {
                    valid = false;
                    context.Error(FontDiagnosticCodes.InvalidDirectory, ContainerMessages.Range, record + TableOffset, 0);
                }
                if(ContainerLayout.InBounds(entry.Start, entry.Length, size) && entry.Length > 0)
                    ranges.Add(entry);
            }
            if(sfntSize != MetadataTables.U32(data, SfntSizeOffset)) {
                valid = false;
                context.Error(FontDiagnosticCodes.InvalidContainer, ContainerMessages.Header, SfntSizeOffset);
            }
            long tablesEnd = directoryEnd;
            foreach(var range in ranges)
                tablesEnd = Math.Max(tablesEnd, range.End);
            long metadataStart = MetadataTables.U32(data, MetadataOffset);
            long privateStart = MetadataTables.U32(data, PrivateOffset);
            long metadataEnd = metadataStart + MetadataTables.U32(data, MetadataLength);
            if((metadataStart != 0 && metadataStart < tablesEnd) ||
                (privateStart != 0 && privateStart < Math.Max(tablesEnd, metadataEnd))) {
                valid = false;
                context.Error(FontDiagnosticCodes.InvalidContainer, ContainerMessages.Range, MetadataOffset);
            }
            valid &= AddOptional(data, context, ranges, directoryEnd, size, MetadataOffset, MetadataLength, MetadataOriginalLength);
            valid &= AddOptional(data, context, ranges, directoryEnd, size, PrivateOffset, PrivateLength, null);
            valid &= ValidateRanges(data.Slice(0, size), context, ranges, directoryEnd);
            if(privateStart != 0 && privateStart + MetadataTables.U32(data, PrivateLength) != size) {
                valid = false;
                context.Error(FontDiagnosticCodes.InvalidContainer, ContainerMessages.Range, PrivateLength);
            }
            var names = ReadMetadata(data, context, tables.GetValueOrDefault(ContainerLayout.NameTag));
            var os2 = ReadMetadata(data, context, tables.GetValueOrDefault(ContainerLayout.Os2Tag));
            var face = context.CreateFace(0, flavor, names, os2, valid);
            context.Faces.Add(face);
        }
        static bool AddOptional(ReadOnlySpan<byte> data, ReadingContext context, List<Entry> ranges,
            long directoryEnd, int size, int offsetField, int lengthField, int? originalField) {
            long offset = MetadataTables.U32(data, offsetField);
            long length = MetadataTables.U32(data, lengthField);
            long original = originalField.HasValue ? MetadataTables.U32(data, originalField.Value) : length;
            if(offset == 0 && length == 0 && original == 0)
                return true;
            bool valid = offset >= directoryEnd && length > 0 && original > 0 &&
                offset % ContainerLayout.Alignment == 0 && ContainerLayout.InBounds(offset, length, size);
            if(!valid)
                context.Error(FontDiagnosticCodes.InvalidContainer, ContainerMessages.Range, offsetField);
            if(length > 0 && ContainerLayout.InBounds(offset, length, size))
                ranges.Add(new Entry(offset, length, 0));
            return valid;
        }
        static bool ValidateRanges(ReadOnlySpan<byte> data, ReadingContext context, List<Entry> ranges, long directoryEnd) {
            ranges.Sort((a, b) => a.Start.CompareTo(b.Start));
            bool valid = true;
            long previousEnd = directoryEnd;
            for(int begin = 0; begin < ranges.Count;) {
                context.CheckCancellation();
                int end = begin + 1;
                long groupEnd = ranges[begin].End;
                while(end < ranges.Count && ranges[end].Start < groupEnd) {
                    groupEnd = Math.Max(groupEnd, ranges[end].End);
                    end++;
                }
                if(end > begin + 1 || ranges[begin].Start < directoryEnd) {
                    valid = false;
                    for(int i = begin; i < end; i++)
                        ranges[i].Readable = false;
                    context.Error(FontDiagnosticCodes.InvalidContainer, ContainerMessages.Range, ranges[begin].Start);
                }
                long gap = ranges[begin].Start - previousEnd;
                if(gap < 0 || gap > 3 || (gap > 0 &&
                    !ContainerLayout.IsZero(data.Slice((int)previousEnd, (int)gap), context))) {
                    valid = false;
                    context.Error(FontDiagnosticCodes.InvalidContainer, ContainerMessages.Range, previousEnd);
                }
                previousEnd = Math.Max(previousEnd, groupEnd);
                begin = end;
            }
            long trailing = data.Length - previousEnd;
            if(trailing < 0 || trailing > 3 || (trailing > 0 &&
                !ContainerLayout.IsZero(data.Slice((int)previousEnd, (int)trailing), context))) {
                valid = false;
                context.Error(FontDiagnosticCodes.InvalidContainer, ContainerMessages.Range, previousEnd);
            }
            return valid;
        }
        static TableMetadata ReadMetadata(ReadOnlySpan<byte> data, ReadingContext context, Entry? entry) {
            if(entry == null)
                return TableMetadata.Empty(FontSectionStatus.Missing);
            if(!entry.Readable)
                return TableMetadata.Empty(FontSectionStatus.Invalid);
            string tag = ContainerLayout.TagText(entry.Tag);
            if(entry.Original > context.Limits.MaxTableBytes) {
                context.Error(FontDiagnosticCodes.LimitExceeded, ContainerMessages.Limit, entry.Start, 0, tag,
                    FontDiagnosticCategory.LimitExceeded);
                return TableMetadata.Empty(FontSectionStatus.NotRead);
            }
            var content = data.Slice((int)entry.Start, (int)entry.Length);
            if(entry.Length == entry.Original)
                return context.ReadTable(entry.Tag, content, new SourceLocation(0, 0), entry.Start);
            try {
                var budget = new DecodeBudget(context, content.Length);
                budget.CheckDeclared(entry.Original);
                using(var compressed = new DecodedBuffer(context, content.Length)) {
                    ContainerLayout.Copy(content, compressed.Bytes, context);
                    using(var output = new DecodedBuffer(context, (int)entry.Original)) {
                        ZlibTableDecoder.Decode(compressed.Bytes, output.Bytes, context);
                        var source = context.NewSource(entry.Start, compressed: true);
                        return context.ReadTable(entry.Tag, output.Bytes, source, 0);
                    }
                }
            }
            catch(DecodeLimitException) {
                context.Error(FontDiagnosticCodes.LimitExceeded, ContainerMessages.Limit, entry.Start, 0, tag,
                    FontDiagnosticCategory.LimitExceeded);
                return TableMetadata.Empty(FontSectionStatus.NotRead);
            }
            catch(InvalidDataException) {
                context.Error(FontDiagnosticCodes.InvalidCompressedData, ContainerMessages.Compression, entry.Start, 0, tag);
                return TableMetadata.Empty(FontSectionStatus.Invalid);
            }
        }
        sealed class Entry {
            internal Entry(long start, long length, uint tag) {
                Start = start;
                Length = length;
                Tag = tag;
            }
            internal long Start { get; }
            internal long Length { get; }
            internal long End { get { return Start + Length; } }
            internal uint Tag { get; }
            internal long Original { get; init; }
            internal bool Readable { get; set; } = true;
        }
    }
}
