using System;
using System.Collections.Generic;
using System.IO;

namespace Fonts.Metadata.Internal {
    internal static partial class Woff2Parser {
        // https://www.w3.org/TR/WOFF2/#table_dir_format
        const int HeaderSize = 48, FlavorOffset = 4, LengthOffset = 8, CountOffset = 12, ReservedOffset = 14;
        const int CompressedSizeOffset = 20, MetadataOffset = 28, PrivateOffset = 40;
        const int ExplicitTag = 63, TransformShift = 6;
        const uint CollectionVersion1 = 0x00010000, CollectionVersion2 = 0x00020000;
        const int Base128Bits = 7, Base128Mask = 0x7f, Base128Continuation = 0x80, Base128MaxBytes = 5;
        const uint Base128OverflowMask = 0xfe000000;
        const int UInt255Word = 253, UInt255High = 254, UInt255Low = 255, UInt255HighBase = 506;
        static readonly string[] knownTags = {
            "cmap", "head", "hhea", "hmtx", "maxp", "name", "OS/2", "post", "cvt ", "fpgm", "glyf", "loca",
            "prep", "CFF ", "VORG", "EBDT", "EBLC", "gasp", "hdmx", "kern", "LTSH", "PCLT", "VDMX", "vhea",
            "vmtx", "BASE", "GDEF", "GPOS", "GSUB", "EBSC", "JSTF", "MATH", "CBDT", "CBLC", "COLR", "CPAL",
            "SVG ", "sbix", "acnt", "avar", "bdat", "bloc", "bsln", "cvar", "fdsc", "feat", "fmtx", "fvar",
            "gvar", "hsty", "just", "lcar", "mort", "morx", "opbd", "prop", "trak", "Zapf", "Silf", "Glat",
            "Gloc", "Feat", "Sill"
        };
        internal static void Read(ReadOnlySpan<byte> data, ReadingContext context) {
            if(data.Length < HeaderSize) {
                context.Error(FontDiagnosticCodes.InvalidContainer, ContainerMessages.Header, 0);
                return;
            }
            uint flavor = MetadataTables.U32(data, FlavorOffset);
            bool collection = flavor == ContainerLayout.CollectionTag;
            if(!collection && !SfntParser.IsSupportedSfntFlavor(flavor)) {
                context.Error(FontDiagnosticCodes.UnsupportedFormat, ContainerMessages.UnsupportedFlavor, FlavorOffset,
                    category: FontDiagnosticCategory.UnsupportedFeature);
                return;
            }
            if(MetadataTables.U32(data, LengthOffset) != data.Length)
                context.Error(FontDiagnosticCodes.InvalidContainer, ContainerMessages.Header, LengthOffset);
            if(MetadataTables.U16(data, ReservedOffset) != 0)
                context.Warning(ContainerMessages.Reserved, ReservedOffset);
            int count = MetadataTables.U16(data, CountOffset);
            if(count == 0) {
                context.Error(FontDiagnosticCodes.InvalidDirectory, ContainerMessages.Header, CountOffset);
                return;
            }
            if(count > context.Limits.MaxWoff2Tables) {
                context.Error(FontDiagnosticCodes.LimitExceeded, ContainerMessages.Limit, CountOffset,
                    category: FontDiagnosticCategory.LimitExceeded);
                return;
            }
            var cursor = new Cursor(data, HeaderSize, context);
            var entries = new List<Table>(count);
            var faces = new List<FaceDirectory>();
            try {
                long length = 0;
                for(int i = 0; i < count; i++) {
                    int record = cursor.Position;
                    int flags = cursor.Byte();
                    int tagIndex = flags & ExplicitTag;
                    uint tag = tagIndex == ExplicitTag ? cursor.U32() : Tag(knownTags[tagIndex]);
                    int transform = flags >> TransformShift;
                    long original = cursor.Base128();
                    bool outlines = tag == ContainerLayout.GlyfTag || tag == ContainerLayout.LocaTag;
                    bool transformed = outlines ? transform != 3 : transform != 0;
                    long stored = transformed ? cursor.Base128() : original;
                    bool supported = outlines ? transform is 0 or 3 :
                        tag == ContainerLayout.HmtxTag ? transform is 0 or 1 : transform == 0;
                    entries.Add(new Table(tag, record, length, original, stored, transform, supported));
                    length += stored;
                }
                if(collection) {
                    if(!ReadCollection(ref cursor, entries, faces, context))
                        return;
                }
                else {
                    var face = new FaceDirectory(0, flavor);
                    if(entries.Count > context.Limits.MaxTablesPerFace) {
                        face.Failure = FontSectionStatus.NotRead;
                        context.Error(FontDiagnosticCodes.LimitExceeded, ContainerMessages.Limit, CountOffset, 0,
                            category: FontDiagnosticCategory.LimitExceeded);
                    }
                    else {
                        for(int i = 0; i < entries.Count; i++)
                            AddReference(face, entries, i, entries[i].Record, context);
                        ValidateTransforms(face, entries, false, context);
                    }
                    faces.Add(face);
                }
                DecodeAndBuild(data, cursor.Position, length, entries, faces, context);
            }
            catch(InvalidDataException) {
                context.Error(FontDiagnosticCodes.InvalidDirectory, ContainerMessages.Header, cursor.Position);
                AddUnavailableFaces(faces, context);
            }
            catch(DecodeLimitException) {
                context.Error(FontDiagnosticCodes.LimitExceeded, ContainerMessages.Limit, cursor.Position,
                    category: FontDiagnosticCategory.LimitExceeded);
                AddUnavailableFaces(faces, context);
            }
        }
        static void AddUnavailableFaces(List<FaceDirectory> faces, ReadingContext context) {
            foreach(var face in faces) {
                var names = TableMetadata.Empty(FontSectionStatus.NotRead);
                var os2 = TableMetadata.Empty(FontSectionStatus.NotRead);
                var result = context.CreateFace(face.Index, face.Flavor, names, os2, false);
                context.Faces.Add(result);
            }
        }
        static bool ReadCollection(ref Cursor cursor, List<Table> entries, List<FaceDirectory> faces, ReadingContext context) {
            uint version = cursor.U32();
            if(version is not CollectionVersion1 and not CollectionVersion2) {
                context.Error(FontDiagnosticCodes.UnsupportedTableVersion, ContainerMessages.CollectionVersion,
                    cursor.Position - sizeof(uint), category: FontDiagnosticCategory.UnsupportedFeature);
                return false;
            }
            int count = cursor.UInt255();
            if(count == 0)
                throw new InvalidDataException(ContainerMessages.Header);
            if(count > context.Limits.MaxFaces)
                context.Error(FontDiagnosticCodes.LimitExceeded, ContainerMessages.Limit, cursor.Position,
                    category: FontDiagnosticCategory.LimitExceeded);
            long references = 0;
            var used = new bool[entries.Count];
            for(int index = 0; index < count; index++) {
                context.CheckCancellation();
                int tableCount = cursor.UInt255();
                uint flavor = cursor.U32();
                bool retained = index < context.Limits.MaxFaces;
                var face = retained ? new FaceDirectory(index, flavor) : null;
                if(face != null) {
                    if(!SfntParser.IsSupportedSfntFlavor(flavor)) {
                        face.Failure = FontSectionStatus.Unsupported;
                        context.Error(FontDiagnosticCodes.UnsupportedFormat, ContainerMessages.UnsupportedFlavor,
                            cursor.Position - sizeof(uint), index, category: FontDiagnosticCategory.UnsupportedFeature);
                    }
                    else if(tableCount == 0) {
                        face.Failure = FontSectionStatus.Invalid;
                        context.Error(FontDiagnosticCodes.InvalidDirectory, ContainerMessages.Header, cursor.Position, index);
                    }
                    else if(tableCount > context.Limits.MaxTablesPerFace) {
                        face.Failure = FontSectionStatus.NotRead;
                        context.Error(FontDiagnosticCodes.LimitExceeded, ContainerMessages.Limit, cursor.Position, index,
                            category: FontDiagnosticCategory.LimitExceeded);
                    }
                }
                for(int i = 0; i < tableCount; i++) {
                    if(++references > context.Limits.MaxWoff2TableReferences)
                        throw new DecodeLimitException();
                    int position = cursor.Position;
                    int reference = cursor.UInt255();
                    if(reference < entries.Count)
                        used[reference] = true;
                    else if(face == null || face.Failure.HasValue)
                        context.Error(FontDiagnosticCodes.InvalidDirectory, ContainerMessages.Header, position, index);
                    if(face != null && !face.Failure.HasValue)
                        AddReference(face, entries, reference, position, context);
                }
                if(face != null) {
                    if(!face.Failure.HasValue)
                        ValidateTransforms(face, entries, true, context);
                    faces.Add(face);
                }
            }
            if(Array.IndexOf(used, false) >= 0)
                context.Error(FontDiagnosticCodes.InvalidDirectory, ContainerMessages.Header, HeaderSize);
            return true;
        }
        static void AddReference(FaceDirectory face, List<Table> entries, int reference, int position, ReadingContext context) {
            if(reference >= entries.Count) {
                face.Valid = false;
                face.AbsenceKnown = false;
                context.Error(FontDiagnosticCodes.InvalidDirectory, ContainerMessages.Header, position, face.Index);
                return;
            }
            var table = entries[reference];
            if(!face.Tables.TryAdd(table.Tag, reference)) {
                face.Tables[table.Tag] = -1;
                face.Valid = false;
                context.Error(FontDiagnosticCodes.InvalidDirectory, ContainerMessages.Duplicate, position, face.Index, ContainerLayout.TagText(table.Tag));
            }
            if(!table.Supported) {
                face.Valid = false;
                context.Error(FontDiagnosticCodes.UnsupportedTransform, ContainerMessages.Transform, table.Record, face.Index,
                    ContainerLayout.TagText(table.Tag), FontDiagnosticCategory.UnsupportedFeature);
            }
        }
        static void ValidateTransforms(FaceDirectory face, List<Table> entries, bool collection, ReadingContext context) {
            int glyf = face.Tables.GetValueOrDefault(ContainerLayout.GlyfTag, -1);
            int loca = face.Tables.GetValueOrDefault(ContainerLayout.LocaTag, -1);
            bool pair = glyf >= 0 && loca >= 0 && glyf < loca &&
                (!entries[glyf].Supported || !entries[loca].Supported || entries[glyf].Transform == entries[loca].Transform) &&
                (!collection || loca == glyf + 1) &&
                (entries[loca].Transform != 0 || entries[loca].Stored == 0);
            bool invalid = (glyf >= 0 || loca >= 0) && !pair;
            int hmtx = face.Tables.GetValueOrDefault(ContainerLayout.HmtxTag, -1);
            if(hmtx >= 0 && entries[hmtx].Transform == 1)
                invalid |= !pair || !face.Tables.ContainsKey(Tag("hhea")) || !face.Tables.ContainsKey(Tag("maxp"));
            if(invalid) {
                face.Valid = false;
                context.Error(FontDiagnosticCodes.InvalidDirectory, ContainerMessages.TransformPair, HeaderSize, face.Index);
            }
        }
        static uint Tag(string value) {
            return (uint)value[0] << 24 | (uint)value[1] << 16 | (uint)value[2] << 8 | value[3];
        }
        sealed class FaceDirectory {
            internal FaceDirectory(int index, uint flavor) {
                Index = index;
                Flavor = flavor;
            }
            internal int Index { get; }
            internal uint Flavor { get; }
            internal Dictionary<uint, int> Tables { get; } = new();
            internal bool Valid { get; set; } = true;
            internal bool AbsenceKnown { get; set; } = true;
            internal FontSectionStatus? Failure { get; set; }
        }
        sealed class Table {
            internal Table(uint tag, int record, long offset, long original, long stored, int transform, bool supported) {
                Tag = tag;
                Record = record;
                Offset = offset;
                Original = original;
                Stored = stored;
                Transform = transform;
                Supported = supported;
            }
            internal uint Tag { get; }
            internal int Record { get; }
            internal long Offset { get; }
            internal long Original { get; }
            internal long Stored { get; }
            internal int Transform { get; }
            internal bool Supported { get; }
            internal DecodedBuffer? Buffer { get; set; }
            internal int Written { get; set; }
            internal FontSectionStatus? Failure { get; set; }
            internal FontDiagnostic? FailureDiagnostic { get; set; }
        }
        ref struct Cursor {
            readonly ReadOnlySpan<byte> bytes;
            readonly ReadingContext context;
            internal Cursor(ReadOnlySpan<byte> bytes, int position, ReadingContext context) {
                this.bytes = bytes;
                Position = position;
                this.context = context;
            }
            internal int Position { get; private set; }
            internal byte Byte() {
                context.CheckCancellation();
                if(Position >= bytes.Length)
                    throw new InvalidDataException(ContainerMessages.Header);
                return bytes[Position++];
            }
            internal uint U32() {
                uint a = Byte(), b = Byte(), c = Byte(), d = Byte();
                return a << 24 | b << 16 | c << 8 | d;
            }
            internal uint Base128() {
                uint value = 0;
                for(int i = 0; i < Base128MaxBytes; i++) {
                    byte current = Byte();
                    if((i == 0 && current == Base128Continuation) || (value & Base128OverflowMask) != 0)
                        throw new InvalidDataException(ContainerMessages.Header);
                    value = (value << Base128Bits) | (uint)(current & Base128Mask);
                    if((current & Base128Continuation) == 0)
                        return value;
                }
                throw new InvalidDataException(ContainerMessages.Header);
            }
            internal int UInt255() {
                int code = Byte();
                if(code == UInt255Word)
                    return (Byte() << 8) | Byte();
                if(code == UInt255High)
                    return Byte() + UInt255HighBase;
                if(code == UInt255Low)
                    return Byte() + UInt255Word;
                return code;
            }
        }
    }
}
