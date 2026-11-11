using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fonts.Metadata.Tests {
    // Synthetic wire data authored for these tests, without font binaries or production parser helpers.
    // Builders write big-endian fields independently with byte shifts; tests mutate explicit record positions.
    internal static class FontFixtures {
        // Independent fixture definitions, not imported from the production reader.
        // https://learn.microsoft.com/en-us/typography/opentype/spec/otff
        // https://learn.microsoft.com/en-us/typography/opentype/spec/name
        internal const uint TrueTypeFlavor = 0x00010000, CollectionVersion1 = 0x00010000, CollectionVersion2 = 0x00020000;
        internal const int SfntHeaderSize = 12, TableRecordSize = 16, TableAlignment = 4, TableCountOffset = 4;
        internal const int TableDataOffsetField = 8, TableLengthField = 12;
        internal const int CollectionHeaderSize = 12, CollectionDsigSize = 12;
        internal const int CollectionVersionOffset = 4, CollectionFaceCountOffset = 8;
        internal const int NameHeaderSize = 6, NameRecordSize = 12, LanguageRecordSize = 4;
        internal const int NameCountOffset = 2, NameStorageOffset = 4;
        internal const int NameEncodingField = 2, NameLanguageField = 4, NameIdField = 6;
        internal const int NameLengthField = 8, NameStringOffsetField = 10;
        internal const int LanguageStringOffsetField = 2;
        internal const int Os2PermissionsOffset = 8, Os2VendorOffset = 58, Os2VendorSize = 4, Os2Version0Size = 78;
        internal const ushort WindowsPlatform = 3, WindowsUnicodeBmpEncoding = 1, EnglishLanguage = 0x0409;
        const int NameFormatWithLanguageTags = 1;
        const int BitsPerByte = 8;
        internal const ushort CopyrightNameId = 0;
        internal const ushort FamilyNameId = 1;
        internal const ushort SubfamilyNameId = 2;
        internal const ushort FullNameId = 4;
        internal const ushort VersionNameId = 5;
        internal const ushort PostScriptNameId = 6;
        internal const ushort TrademarkNameId = 7;
        internal const ushort ManufacturerNameId = 8;
        internal const ushort DesignerNameId = 9;
        internal const ushort DescriptionNameId = 10;
        internal const ushort VendorUrlNameId = 11;
        internal const ushort DesignerUrlNameId = 12;
        internal const ushort LicenseDescriptionNameId = 13;
        internal const ushort LicenseUrlNameId = 14;
        internal const ushort TypographicFamilyNameId = 16;
        internal const ushort TypographicSubfamilyNameId = 17;

        internal sealed record Name(ushort Id, byte[] Bytes, ushort Platform = WindowsPlatform, ushort Encoding = WindowsUnicodeBmpEncoding, ushort Language = EnglishLanguage) {
            internal static Name Text(ushort id, string value, ushort platform = WindowsPlatform, ushort encoding = WindowsUnicodeBmpEncoding, ushort language = EnglishLanguage) {
                byte[] bytes = System.Text.Encoding.BigEndianUnicode.GetBytes(value);
                return new Name(id, bytes, platform, encoding, language);
            }
        }
        internal static void U16(byte[] data, int offset, int value) {
            data[offset] = (byte)(value >> BitsPerByte);
            data[offset + 1] = (byte)value;
        }
        internal static void U32(byte[] data, int offset, uint value) {
            data[offset] = (byte)(value >> (3 * BitsPerByte));
            data[offset + 1] = (byte)(value >> (2 * BitsPerByte));
            data[offset + 2] = (byte)(value >> BitsPerByte);
            data[offset + 3] = (byte)value;
        }
        internal static int ReadOffset(byte[] data, int position) {
            return data[position] << (3 * BitsPerByte) | data[position + 1] << (2 * BitsPerByte) | data[position + 2] << BitsPerByte | data[position + 3];
        }
        internal static byte[] Names(params Name[] names) {
            return NamesWithLanguages(names, null);
        }
        internal static byte[] NamesWithLanguages(Name[] names, string[]? languages) {
            int storage = NameHeaderSize + names.Length * NameRecordSize + (languages == null ? 0 : sizeof(ushort) + languages.Length * LanguageRecordSize);
            var strings = new List<byte>();
            var header = new byte[storage];
            U16(header, 0, languages == null ? 0 : NameFormatWithLanguageTags);
            U16(header, NameCountOffset, names.Length);
            U16(header, NameStorageOffset, storage);
            for(int i = 0; i < names.Length; i++) {
                Name name = names[i];
                int pos = NameHeaderSize + i * NameRecordSize;
                U16(header, pos, name.Platform);
                U16(header, pos + NameEncodingField, name.Encoding);
                U16(header, pos + NameLanguageField, name.Language);
                U16(header, pos + NameIdField, name.Id);
                U16(header, pos + NameLengthField, name.Bytes.Length);
                U16(header, pos + NameStringOffsetField, strings.Count);
                strings.AddRange(name.Bytes);
            }
            if(languages != null) {
                int pos = NameHeaderSize + names.Length * NameRecordSize;
                U16(header, pos, languages.Length);
                for(int i = 0; i < languages.Length; i++) {
                    byte[] bytes = Encoding.BigEndianUnicode.GetBytes(languages[i]);
                    U16(header, pos + sizeof(ushort) + i * LanguageRecordSize, bytes.Length);
                    U16(header, pos + sizeof(ushort) + i * LanguageRecordSize + LanguageStringOffsetField, strings.Count);
                    strings.AddRange(bytes);
                }
            }
            return header.Concat(strings).ToArray();
        }
        internal static byte[] Os2(ushort permissions = 0, string vendor = "TEST", int length = Os2Version0Size, ushort version = 0) {
            var data = new byte[length];
            if(length >= sizeof(ushort))
                U16(data, 0, version);
            if(length >= Os2PermissionsOffset + sizeof(ushort))
                U16(data, Os2PermissionsOffset, permissions);
            if(length >= Os2VendorOffset + Os2VendorSize)
                Encoding.ASCII.GetBytes(vendor.PadRight(Os2VendorSize).Substring(0, Os2VendorSize)).CopyTo(data, Os2VendorOffset);
            return data;
        }
        internal static byte[] Sfnt(params (string Tag, byte[] Data)[] tables) {
            return Sfnt(TrueTypeFlavor, tables);
        }
        internal static byte[] Sfnt(uint flavor, params (string Tag, byte[] Data)[] tables) {
            return CollectionCore([(flavor, tables)], false, CollectionVersion1);
        }
        internal static byte[] Collection(params (string Tag, byte[] Data)[][] faces) {
            return CollectionCore(faces.Select(x => (TrueTypeFlavor, x)).ToArray(), true, CollectionVersion1);
        }
        internal static byte[] CollectionV2(params (string Tag, byte[] Data)[][] faces) {
            return CollectionCore(faces.Select(x => (TrueTypeFlavor, x)).ToArray(), true, CollectionVersion2);
        }
        static byte[] CollectionCore((uint Flavor, (string Tag, byte[] Data)[] Tables)[] faces, bool collection, uint version) {
            int header = collection ? CollectionHeaderSize + faces.Length * sizeof(uint) + (version == CollectionVersion2 ? CollectionDsigSize : 0) : 0;
            int cursor = header;
            var directories = new List<int>();
            foreach(var face in faces) {
                directories.Add(cursor);
                cursor += SfntHeaderSize + face.Tables.Length * TableRecordSize;
            }
            var records = new List<(int Position, string Tag, int Offset, byte[] Data)>();
            for(int i = 0; i < faces.Length; i++) {
                var tables = faces[i].Tables.OrderBy(x => x.Tag, StringComparer.Ordinal).ToArray();
                for(int j = 0; j < tables.Length; j++) {
                    cursor = AlignTable(cursor);
                    records.Add((directories[i] + SfntHeaderSize + j * TableRecordSize, tables[j].Tag, cursor, tables[j].Data));
                    cursor += tables[j].Data.Length;
                }
            }
            var result = new byte[cursor];
            if(collection) {
                Encoding.ASCII.GetBytes("ttcf").CopyTo(result, 0);
                U32(result, CollectionVersionOffset, version);
                U32(result, CollectionFaceCountOffset, (uint)faces.Length);
                for(int i = 0; i < faces.Length; i++)
                    U32(result, CollectionHeaderSize + i * sizeof(uint), (uint)directories[i]);
            }
            for(int i = 0; i < faces.Length; i++) {
                U32(result, directories[i], faces[i].Flavor);
                U16(result, directories[i] + TableCountOffset, faces[i].Tables.Length);
            }
            foreach(var record in records) {
                Encoding.ASCII.GetBytes(record.Tag).CopyTo(result, record.Position);
                U32(result, record.Position + TableDataOffsetField, (uint)record.Offset);
                U32(result, record.Position + TableLengthField, (uint)record.Data.Length);
                record.Data.CopyTo(result, record.Offset);
            }
            return result;
        }
        static int AlignTable(int offset) {
            return (offset + TableAlignment - 1) & ~(TableAlignment - 1);
        }
        internal static int NameRecordPosition(int index) {
            return NameHeaderSize + index * NameRecordSize;
        }
        internal static int CollectionFacePosition(int index) {
            return CollectionHeaderSize + index * sizeof(uint);
        }
        internal static int TableOffsetPosition(int index) {
            return SfntHeaderSize + index * TableRecordSize + TableDataOffsetField;
        }
        internal static int TableLengthPosition(int index) {
            return SfntHeaderSize + index * TableRecordSize + TableLengthField;
        }
        internal static byte[] Example() {
            return Sfnt(("name", Names(Name.Text(FamilyNameId, "Example"))), ("OS/2", Os2()));
        }
    }
}
