using System;
using System.Buffers.Binary;
using System.Text;

namespace Fonts.Metadata.Internal {
    internal static class ContainerLayout {
        internal const int Alignment = 4, SfntHeaderSize = 12, SfntRecordSize = 16;
        internal static readonly uint NameTag = BinaryPrimitives.ReadUInt32BigEndian("name"u8);
        internal static readonly uint Os2Tag = BinaryPrimitives.ReadUInt32BigEndian("OS/2"u8);
        internal static readonly uint GlyfTag = BinaryPrimitives.ReadUInt32BigEndian("glyf"u8);
        internal static readonly uint LocaTag = BinaryPrimitives.ReadUInt32BigEndian("loca"u8);
        internal static readonly uint HmtxTag = BinaryPrimitives.ReadUInt32BigEndian("hmtx"u8);
        internal static readonly uint CollectionTag = BinaryPrimitives.ReadUInt32BigEndian("ttcf"u8);
        internal static long Align(long value) {
            return (value + Alignment - 1) & ~(Alignment - 1L);
        }
        internal static bool InBounds(long offset, long length, long size) {
            return offset >= 0 && length >= 0 && offset <= size && length <= size - offset;
        }
        internal static bool IsMetadata(uint tag) {
            return tag == NameTag || tag == Os2Tag;
        }
        internal static string TagText(uint tag) {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, tag);
            return Encoding.Latin1.GetString(bytes);
        }
        internal static void Copy(ReadOnlySpan<byte> source, Span<byte> target, ReadingContext context) {
            for(int offset = 0; offset < source.Length;) {
                context.CheckCancellation();
                int length = Math.Min(ReadingContext.CopyChunkSize, source.Length - offset);
                source.Slice(offset, length).CopyTo(target.Slice(offset, length));
                offset += length;
            }
        }
        internal static bool IsZero(ReadOnlySpan<byte> bytes, ReadingContext context) {
            for(int i = 0; i < bytes.Length; i++) {
                if((i & (ReadingContext.CopyChunkSize - 1)) == 0)
                    context.CheckCancellation();
                if(bytes[i] != 0)
                    return false;
            }
            return true;
        }
    }
    internal static class ContainerMessages {
        internal const string Header = "The container header, declared size or directory is invalid.";
        internal const string Range = "Container blocks overlap, have invalid padding or extend outside the input.";
        internal const string UnsupportedFlavor = "The contained SFNT flavor is unsupported.";
        internal const string Limit = "A configured container processing budget prevented complete extraction.";
        internal const string Compression = "The compressed stream is invalid, incomplete or disagrees with its declared input/output length.";
        internal const string Duplicate = "A face contains duplicate table tags or table references.";
        internal const string Transform = "The table transform version is unsupported.";
        internal const string TransformPair = "Related transformed tables do not form a valid pair for this face.";
        internal const string Reserved = "The WOFF2 reserved field is nonzero; metadata remains readable.";
        internal const string Flags = "The payload processing flags are unsupported.";
        internal const string EotHeader = "An EOT header field, string, checksum or separator is invalid.";
        internal const string CollectionVersion = "The WOFF2 collection version is unsupported.";
        internal const string MtxVersion = "The MTX payload version is unsupported.";
    }
}
