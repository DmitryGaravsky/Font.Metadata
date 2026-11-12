using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Fonts.Metadata.Internal {
    internal static partial class Woff2Parser {
        static void DecodeAndBuild(ReadOnlySpan<byte> data, int start, long expected,
            List<Table> entries, List<FaceDirectory> faces, ReadingContext context) {
            long compressedLength = MetadataTables.U32(data, CompressedSizeOffset);
            ValidateLayout(data, start, compressedLength, context);
            bool complete = false;
            var stopped = FontSectionStatus.Invalid;
            var selected = new SortedSet<int>();
            foreach(var face in faces) {
                if(face.Failure.HasValue)
                    continue;
                foreach(var pair in face.Tables) {
                    if(ContainerLayout.IsMetadata(pair.Key) && pair.Value >= 0)
                        selected.Add(pair.Value);
                }
            }
            var buffers = new List<Table>();
            var source = context.NewSource(start, compressed: true);
            try {
                try {
                    if(compressedLength <= 0 || compressedLength > int.MaxValue)
                        throw new InvalidDataException(ContainerMessages.Compression);
                    int available = AvailableCompressedLength(data, start, compressedLength);
                    var budget = new DecodeBudget(context, available);
                    budget.CheckDeclared(expected);
                    foreach(int index in selected) {
                        context.CheckCancellation();
                        var table = entries[index];
                        if(!table.Supported || table.Transform != 0) {
                            table.Failure = FontSectionStatus.Unsupported;
                            continue;
                        }
                        try {
                            if(table.Original > context.Limits.MaxTableBytes)
                                throw new DecodeLimitException();
                            table.Buffer = new DecodedBuffer(context, (int)table.Original);
                            buffers.Add(table);
                        }
                        catch(DecodeLimitException) {
                            table.Failure = FontSectionStatus.NotRead;
                            table.FailureDiagnostic = new FontDiagnostic(FontDiagnosticCodes.LimitExceeded,
                                FontDiagnosticSeverity.Error, FontDiagnosticCategory.LimitExceeded, ContainerMessages.Limit,
                                tableTag: ContainerLayout.TagText(table.Tag), byteOffset: table.Record);
                        }
                    }
                    // Even with no selected tables, consume and validate the entire Brotli stream.
                    Decode(data.Slice(start, available), expected, buffers, context, budget);
                    complete = available == compressedLength;
                    if(available != compressedLength)
                        throw new InvalidDataException(ContainerMessages.Compression);
                }
                catch(DecodeLimitException) {
                    stopped = FontSectionStatus.NotRead;
                    context.Error(FontDiagnosticCodes.LimitExceeded, ContainerMessages.Limit, start,
                        category: FontDiagnosticCategory.LimitExceeded);
                }
                catch(InvalidDataException) {
                    context.Error(FontDiagnosticCodes.InvalidCompressedData, ContainerMessages.Compression, start);
                }
                foreach(var face in faces) {
                    var names = GetMetadata(face, ContainerLayout.NameTag, entries, source, complete, stopped, context);
                    var os2 = GetMetadata(face, ContainerLayout.Os2Tag, entries, source, complete, stopped, context);
                    var result = context.CreateFace(face.Index, face.Flavor, names, os2, face.Valid);
                    context.Faces.Add(result);
                }
            }
            finally {
                foreach(var table in buffers)
                    table.Buffer!.Dispose();
            }
        }
        static TableMetadata GetMetadata(FaceDirectory face, uint tag, List<Table> entries, SourceLocation source,
            bool complete, FontSectionStatus stopped, ReadingContext context) {
            if(face.Failure.HasValue)
                return TableMetadata.Empty(face.Failure.Value);
            if(!face.Tables.TryGetValue(tag, out int index))
                return TableMetadata.Empty(face.AbsenceKnown ? FontSectionStatus.Missing : FontSectionStatus.NotRead);
            if(index < 0)
                return TableMetadata.Empty(FontSectionStatus.Invalid);
            var table = entries[index];
            if(table.Failure.HasValue) {
                if(table.FailureDiagnostic != null)
                    context.Diagnostics.Add(table.FailureDiagnostic.ForFace(face.Index));
                return TableMetadata.Empty(table.Failure.Value);
            }
            if(table.Buffer == null || table.Written != table.Stored)
                return TableMetadata.Empty(stopped);
            return context.ReadTable(tag, table.Buffer.Bytes, source, table.Offset, complete);
        }
        static void Decode(ReadOnlySpan<byte> input, long expected, List<Table> selected,
            ReadingContext context, DecodeBudget budget) {
            var decoder = new BrotliDecoder();
            try {
                var scratch = new byte[ReadingContext.CopyChunkSize];
                int source = 0, selectedIndex = 0;
                long produced = 0;
                while(true) {
                    context.CheckCancellation();
                    int inputLength = Math.Min(ReadingContext.CopyChunkSize, input.Length - source);
                    int outputLength = budget.OutputSize(scratch.Length);
                    long remaining = budget.Remaining;
                    var status = decoder.Decompress(input.Slice(source, inputLength), scratch.AsSpan(0, outputLength),
                        out int consumed, out int written);
                    source += consumed;
                    int usable = (int)Math.Min(written, remaining);
                    CopyTables(scratch.AsSpan(0, usable), produced, selected, ref selectedIndex);
                    produced += written;
                    budget.Charge(written);
                    if(produced > expected)
                        throw new InvalidDataException(ContainerMessages.Compression);
                    if(status == OperationStatus.Done) {
                        if(source != input.Length || produced != expected)
                            throw new InvalidDataException(ContainerMessages.Compression);
                        return;
                    }
                    if(status == OperationStatus.InvalidData || (consumed == 0 && written == 0) ||
                        (status == OperationStatus.NeedMoreData && source == input.Length))
                        throw new InvalidDataException(ContainerMessages.Compression);
                }
            }
            finally {
                decoder.Dispose();
            }
        }
        static void CopyTables(ReadOnlySpan<byte> bytes, long offset, List<Table> tables, ref int index) {
            long end = offset + bytes.Length;
            // Ordered ranges and a monotone cursor avoid scanning every selected table for every chunk.
            while(index < tables.Count && tables[index].Offset < end) {
                var table = tables[index];
                long tableEnd = table.Offset + table.Stored;
                long begin = Math.Max(offset, table.Offset);
                long finish = Math.Min(end, tableEnd);
                if(finish > begin) {
                    int length = (int)(finish - begin);
                    bytes.Slice((int)(begin - offset), length).CopyTo(table.Buffer!.Bytes.AsSpan((int)(begin - table.Offset)));
                    table.Written += length;
                }
                if(tableEnd > end)
                    break;
                index++;
            }
        }
        // Recovery may use only bytes that exist and are not claimed by a bounded optional block.
        // Missing bytes cannot increase the expansion budget, even if totalCompressedSize claims them.
        static int AvailableCompressedLength(ReadOnlySpan<byte> data, int start, long declaredLength) {
            long end = Math.Min(data.Length, start + declaredLength);
            for(int field = MetadataOffset; field <= PrivateOffset; field += PrivateOffset - MetadataOffset) {
                long offset = MetadataTables.U32(data, field);
                long length = MetadataTables.U32(data, field + sizeof(uint));
                if(offset != 0 && length > 0 && ContainerLayout.InBounds(offset, length, data.Length) &&
                    offset < end && offset + length > start)
                    end = Math.Min(end, Math.Max(start, offset));
            }
            return (int)(end - start);
        }
        static void ValidateLayout(ReadOnlySpan<byte> data, int compressedStart, long compressedLength, ReadingContext context) {
            long end = compressedStart + compressedLength;
            bool valid = compressedLength > 0 && ContainerLayout.InBounds(compressedStart, compressedLength, data.Length);
            for(int field = MetadataOffset; field <= PrivateOffset; field += PrivateOffset - MetadataOffset) {
                long offset = MetadataTables.U32(data, field);
                long length = MetadataTables.U32(data, field + sizeof(uint));
                long original = field == MetadataOffset ? MetadataTables.U32(data, field + 2 * sizeof(uint)) : length;
                if(offset == 0) {
                    valid &= length == 0 && original == 0;
                    continue;
                }
                valid &= length > 0 && original > 0 && offset % ContainerLayout.Alignment == 0 &&
                    ContainerLayout.InBounds(offset, length, data.Length) && Padding(data, end, offset, context);
                end = offset + length;
            }
            bool hasPrivate = MetadataTables.U32(data, PrivateOffset) != 0;
            valid &= hasPrivate ? end == data.Length : Padding(data, end, data.Length, context);
            if(!valid)
                context.Error(FontDiagnosticCodes.InvalidContainer, ContainerMessages.Range, compressedStart);
        }
        static bool Padding(ReadOnlySpan<byte> data, long start, long end, ReadingContext context) {
            long length = end - start;
            return length is >= 0 and <= 3 && ContainerLayout.InBounds(start, length, data.Length) &&
                ContainerLayout.IsZero(data.Slice((int)start, (int)length), context);
        }
    }
}
