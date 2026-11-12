using System;
using System.Buffers.Binary;
using System.Text;

namespace Fonts.Metadata.Internal {
    internal static class EotParser {
        // https://www.w3.org/submissions/EOT/#FileFormat
        const int FixedSize = 80, FontSizeField = 4, VersionField = 8, FlagsField = 12, PermissionsField = 32;
        const int ReservedStart = 64, ReservedEnd = 80, MtxHeaderSize = 10;
        const uint Version1 = 0x00010000, Version2 = 0x00020001, Version3 = 0x00020002;
        const uint Compressed = 4, Eudc = 0x20, Xor = 0x10000000;
        const uint Subset = 1, FailIfVariationSimulated = 0x10, ValidationTests = 0x40, WebObject = 0x80;
        const uint KnownFlags = Subset | Compressed | FailIfVariationSimulated | Eudc | ValidationTests | WebObject | Xor;
        const byte XorKey = 0x50, MtxVersion = 3;
        const uint RootChecksumKey = 0x50475342;
        static readonly Encoding Unicode = new UnicodeEncoding(false, false, true);
        internal static void Read(ReadOnlySpan<byte> data, ReadingContext context) {
            if(data.Length < FixedSize) {
                context.EotStatus = FontSectionStatus.Invalid;
                context.Error(FontDiagnosticCodes.InvalidEotHeader, ContainerMessages.EotHeader, data.Length);
                return;
            }
            uint total = U32(data, 0), mainSize = U32(data, FontSizeField);
            var facts = new HeaderFacts {
                FormatVersion = U32(data, VersionField),
                Flags = U32(data, FlagsField),
                Permissions = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(PermissionsField))
            };
            bool valid = total == data.Length && mainSize > 0 && mainSize <= total - Math.Min(total, FixedSize);
            int mainStart = valid ? (int)(total - mainSize) : -1;
            var reader = new HeaderReader(data, valid ? mainStart : FixedSize, context, facts);
            for(int field = ReservedStart; field < ReservedEnd; field += sizeof(uint)) {
                if(U32(data, field) != 0)
                    reader.Error(field);
            }
            if(facts.FormatVersion == Version1 && (facts.Flags & Eudc) != 0)
                reader.Error(FlagsField);
            if(!valid)
                reader.Error(FontSizeField);
            else
                reader.Read();
            context.Eot = facts.Freeze();
            context.EotStatus = reader.Incomplete ? FontSectionStatus.Partial : FontSectionStatus.Complete;
            context.HasUsableMetadata = true;
            context.IncompleteSections |= reader.Incomplete;
            if(mainStart >= FixedSize)
                ReadPayload(data.Slice(mainStart, (int)mainSize), mainStart, facts.Flags, 0, FontFaceSource.EotMain, context);
            if(reader.EudcStart.HasValue && facts.EudcSize > 0) {
                if(context.Limits.MaxFaces < 2) {
                    context.Error(FontDiagnosticCodes.LimitExceeded, ContainerMessages.Limit, reader.EudcStart, 1,
                        category: FontDiagnosticCategory.LimitExceeded);
                }
                else {
                    int offset = reader.EudcStart.Value;
                    ReadPayload(data.Slice(offset, (int)facts.EudcSize.Value), offset, facts.EudcFlags!.Value,
                        1, FontFaceSource.EotEudc, context);
                }
            }
        }
        static uint U32(ReadOnlySpan<byte> data, int offset) {
            return BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset));
        }
        static void ReadPayload(ReadOnlySpan<byte> payload, int offset, uint flags, int index,
            FontFaceSource kind, ReadingContext context) {
            var source = context.NewSource(offset);
            if((flags & ~KnownFlags) != 0) {
                context.Error(FontDiagnosticCodes.UnsupportedFlags, ContainerMessages.Flags, offset, index,
                    category: FontDiagnosticCategory.UnsupportedFeature);
                AddUnavailable(index, kind, FontSectionStatus.Unsupported, context);
                return;
            }
            try {
                if((flags & (Xor | Compressed)) == 0) {
                    ReadFace(payload, source, index, kind, true, context);
                    return;
                }
                using(var copy = new DecodedBuffer(context, payload.Length)) {
                    ContainerLayout.Copy(payload, copy.Bytes, context);
                    if((flags & Xor) != 0) {
                        for(int i = 0; i < copy.Bytes.Length; i++) {
                            if((i & (ReadingContext.CopyChunkSize - 1)) == 0)
                                context.CheckCancellation();
                            copy.Bytes[i] ^= XorKey;
                        }
                    }
                    if((flags & Compressed) == 0) {
                        ReadFace(copy.Bytes, source, index, kind, true, context);
                        return;
                    }
                    if(copy.Bytes.Length >= MtxHeaderSize && copy.Bytes[0] != MtxVersion) {
                        context.Error(FontDiagnosticCodes.UnsupportedFormat, ContainerMessages.MtxVersion, offset, index,
                            category: FontDiagnosticCategory.UnsupportedFeature);
                        AddUnavailable(index, kind, FontSectionStatus.Unsupported, context);
                        return;
                    }
                    using(var decoded = MtxDecoder.Decode(copy.Bytes, context, source, index)) {
                        if(decoded.First == null) {
                            AddUnavailable(index, kind, decoded.Limited ? FontSectionStatus.NotRead : FontSectionStatus.Invalid, context);
                            return;
                        }
                        var ctfSource = context.NewSource(offset + MtxHeaderSize, compressed: true);
                        ReadFace(decoded.First.Content, ctfSource, index, kind, decoded.Complete, context);
                    }
                }
            }
            catch(DecodeLimitException) {
                context.Error(FontDiagnosticCodes.LimitExceeded, ContainerMessages.Limit, offset, index,
                    category: FontDiagnosticCategory.LimitExceeded);
                AddUnavailable(index, kind, FontSectionStatus.NotRead, context);
            }
        }
        static void ReadFace(ReadOnlySpan<byte> payload, SourceLocation source, int index, FontFaceSource kind,
            bool complete, ReadingContext context) {
            uint? flavor = payload.Length >= sizeof(uint) ? MetadataTables.U32(payload, 0) : null;
            if(flavor.HasValue && !SfntParser.IsSupportedSfntFlavor(flavor.Value)) {
                context.Error(FontDiagnosticCodes.UnsupportedFormat, ContainerMessages.UnsupportedFlavor, source.Offset, index,
                    category: FontDiagnosticCategory.UnsupportedFeature);
                AddUnavailable(index, kind, FontSectionStatus.Unsupported, context, flavor);
                return;
            }
            // CTF keeps SFNT metadata directories and may contain empty loca; the existing metadata-only
            // SFNT validator already permits zero-length unrelated tables without relaxing its range checks.
            var parser = new SfntParser(payload, context, source, kind, complete);
            var face = parser.ReadSingleFace(index);
            context.Faces.Add(face);
        }
        static void AddUnavailable(int index, FontFaceSource source, FontSectionStatus status, ReadingContext context, uint? flavor = null) {
            var names = TableMetadata.Empty(status);
            var os2 = TableMetadata.Empty(status);
            var face = context.CreateFace(index, flavor, names, os2, false, source);
            context.Faces.Add(face);
        }
        sealed class HeaderFacts {
            internal uint FormatVersion { get; init; }
            internal uint Flags { get; init; }
            internal ushort Permissions { get; init; }
            internal string? Family { get; set; }
            internal string? Style { get; set; }
            internal string? FullName { get; set; }
            internal string? Version { get; set; }
            internal uint? EudcCodePage { get; set; }
            internal uint? EudcFlags { get; set; }
            internal uint? EudcSize { get; set; }
            internal FontMetadata.EotHeader Freeze() {
                return new FontMetadata.EotHeader(FormatVersion, Flags, Permissions, Family, Style, FullName,
                    Version, EudcCodePage, EudcFlags, EudcSize);
            }
        }
        sealed class FramingException : Exception {
            internal FramingException() {
            }
        }
        ref struct HeaderReader {
            readonly ReadOnlySpan<byte> data;
            readonly int end;
            readonly ReadingContext context;
            readonly HeaderFacts facts;
            int position;
            long stringBytes;
            internal HeaderReader(ReadOnlySpan<byte> data, int end, ReadingContext context, HeaderFacts facts) {
                this.data = data;
                this.end = end;
                this.context = context;
                this.facts = facts;
                position = FixedSize;
            }
            internal bool Incomplete { get; private set; }
            internal int? EudcStart { get; private set; }
            internal void Error(int offset) {
                Incomplete = true;
                context.Error(FontDiagnosticCodes.InvalidEotHeader, ContainerMessages.EotHeader, offset);
            }
            internal void Read() {
                try {
                    Padding();
                    facts.Family = String(true, out _);
                    Padding();
                    facts.Style = String(true, out _);
                    Padding();
                    facts.Version = String(true, out _);
                    Padding();
                    facts.FullName = String(true, out _);
                    if(facts.FormatVersion != Version1) {
                        Padding();
                        String(false, out uint checksum);
                        if(facts.FormatVersion == Version3) {
                            int checksumOffset = position;
                            if(UInt32() != checksum)
                                Error(checksumOffset);
                            facts.EudcCodePage = UInt32();
                            Padding();
                            int signatureSize = UInt16();
                            Advance(signatureSize);
                            facts.EudcFlags = UInt32();
                            facts.EudcSize = UInt32();
                            int start = position;
                            Advance(facts.EudcSize.Value);
                            EudcStart = start;
                        }
                    }
                    if(position != end)
                        Error(position);
                }
                catch(FramingException) {
                    Error(position);
                }
            }
            void Need(long length) {
                context.CheckCancellation();
                if(length < 0 || length > end - (long)position)
                    throw new FramingException();
            }
            void Advance(long length) {
                Need(length);
                position += (int)length;
            }
            ushort UInt16() {
                Need(sizeof(ushort));
                ushort value = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(position));
                position += sizeof(ushort);
                return value;
            }
            uint UInt32() {
                Need(sizeof(uint));
                uint value = U32(data, position);
                position += sizeof(uint);
                return value;
            }
            void Padding() {
                if(UInt16() != 0)
                    throw new FramingException();
            }
            string? String(bool retain, out uint checksum) {
                int length = UInt16();
                Need(length);
                var value = data.Slice(position, length);
                int start = position;
                position += length;
                checksum = 0;
                for(int i = 0; i < value.Length; i++) {
                    if((i & (ReadingContext.CopyChunkSize - 1)) == 0)
                        context.CheckCancellation();
                    checksum += value[i];
                }
                checksum ^= RootChecksumKey;
                if(length > context.Limits.MaxNameStringBytes - stringBytes ||
                    length > context.Limits.MaxTotalNameStringBytes - context.NameStringBytes) {
                    Incomplete = true;
                    context.Error(FontDiagnosticCodes.LimitExceeded, ContainerMessages.Limit, start,
                        category: FontDiagnosticCategory.LimitExceeded);
                    return null;
                }
                stringBytes += length;
                context.NameStringBytes += length;
                if((length & 1) != 0) {
                    Error(start);
                    return null;
                }
                try {
                    // GetChars preserves a pending high surrogate between chunks; GetCharCount does not.
                    var decoder = Unicode.GetDecoder();
                    Span<char> characters = stackalloc char[ReadingContext.CopyChunkSize / sizeof(char) + 1];
                    for(int i = 0; i < value.Length; i += ReadingContext.CopyChunkSize) {
                        context.CheckCancellation();
                        int count = Math.Min(ReadingContext.CopyChunkSize, value.Length - i);
                        decoder.GetChars(value.Slice(i, count), characters, i + count == value.Length);
                    }
                    return retain ? Unicode.GetString(value) : null;
                }
                catch(DecoderFallbackException) {
                    Error(start);
                    return null;
                }
            }
        }
    }
}
