using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Tests {
    internal static class WebFontFixtures {
        const int WoffHeaderSize = 44, WoffRecordSize = 20, Woff2HeaderSize = 48, EotFixedSize = 80;
        internal sealed record Table(string Tag, byte[] Data, bool Compressed = false, int Transform = 0, uint? Original = null);
        internal static byte[] NameData(string family = "Example Sans", string license = "MIT") {
            return Names(Name.Text(1, family), Name.Text(13, license));
        }
        internal static byte[] Compress(byte[] data, bool brotli) {
            using(var output = new MemoryStream()) {
                using(Stream encoder = brotli
                    ? new BrotliStream(output, CompressionLevel.SmallestSize, true)
                    : new ZLibStream(output, CompressionLevel.SmallestSize, true)) {
                    encoder.Write(data);
                }
                return output.ToArray();
            }
        }
        internal static byte[] Woff(params Table[] tables) {
            tables = tables.OrderBy(t => t.Tag, StringComparer.Ordinal).ToArray();
            var result = new List<byte>(new byte[WoffHeaderSize + tables.Length * WoffRecordSize]);
            uint sfntSize = (uint)(12 + tables.Length * 16);
            for(int index = 0; index < tables.Length; index++) {
                var table = tables[index];
                byte[] stored = table.Compressed ? Compress(table.Data, false) : table.Data;
                int record = WoffHeaderSize + index * WoffRecordSize;
                byte[] directory = result.ToArray();
                Encoding.ASCII.GetBytes(table.Tag).CopyTo(directory, record);
                U32(directory, record + 4, (uint)result.Count);
                U32(directory, record + 8, (uint)stored.Length);
                U32(directory, record + 12, (uint)table.Data.Length);
                result = directory.ToList();
                result.AddRange(stored);
                while(result.Count % 4 != 0)
                    result.Add(0);
                sfntSize += (uint)((table.Data.Length + 3) & ~3);
            }
            byte[] bytes = result.ToArray();
            Encoding.ASCII.GetBytes("wOFF").CopyTo(bytes, 0);
            U32(bytes, 4, 0x00010000);
            U32(bytes, 8, (uint)bytes.Length);
            U16(bytes, 12, tables.Length);
            U32(bytes, 16, sfntSize);
            return bytes;
        }
        internal static byte[] Woff2(Table[] tables, ushort[][]? faces = null, Func<int, byte[]>? shortEncoding = null) {
            var directory = new List<byte>();
            var decoded = new List<byte>();
            foreach(var table in tables) {
                directory.Add((byte)(63 | table.Transform << 6));
                directory.AddRange(Encoding.ASCII.GetBytes(table.Tag));
                Base128(directory, table.Original ?? (uint)table.Data.Length);
                bool transformed = table.Tag is "glyf" or "loca" ? table.Transform != 3 : table.Transform != 0;
                if(transformed)
                    Base128(directory, (uint)table.Data.Length);
                decoded.AddRange(table.Data);
            }
            if(faces != null) {
                directory.AddRange(new byte[] { 0, 1, 0, 0 });
                directory.AddRange(shortEncoding?.Invoke(faces.Length) ?? UInt255(faces.Length));
                foreach(var face in faces) {
                    directory.AddRange(shortEncoding?.Invoke(face.Length) ?? UInt255(face.Length));
                    directory.AddRange(new byte[] { 0, 1, 0, 0 });
                    foreach(int reference in face)
                        directory.AddRange(shortEncoding?.Invoke(reference) ?? UInt255(reference));
                }
            }
            byte[] compressed = Compress(decoded.ToArray(), true);
            var header = new byte[Woff2HeaderSize];
            Encoding.ASCII.GetBytes("wOF2").CopyTo(header, 0);
            U32(header, 4, faces == null ? 0x00010000u : 0x74746366u);
            U32(header, 8, (uint)(Woff2HeaderSize + directory.Count + compressed.Length));
            U16(header, 12, tables.Length);
            U32(header, 16, (uint)(12 + tables.Length * 16 + decoded.Count));
            U32(header, 20, (uint)compressed.Length);
            return header.Concat(directory).Concat(compressed).ToArray();
        }
        internal static byte[] UInt255(int value) {
            if(value < 253)
                return [(byte)value];
            if(value < 506)
                return [255, (byte)(value - 253)];
            return [253, (byte)(value >> 8), (byte)value];
        }
        static void Base128(List<byte> bytes, uint value) {
            var encoded = new List<byte> { (byte)(value & 127) };
            while((value >>= 7) != 0)
                encoded.Insert(0, (byte)(128 | (value & 127)));
            bytes.AddRange(encoded);
        }
        internal static byte[] Eot(byte[] payload, uint version = 0x00020002, uint flags = 0,
            byte[]? eudc = null, uint eudcFlags = 0, string family = "Container Family", ushort permissions = 2) {
            using(var output = new MemoryStream()) {
                using(var writer = new BinaryWriter(output, Encoding.Unicode, true)) {
                    writer.Write(new byte[EotFixedSize]);
                    HeaderString(writer, family);
                    HeaderString(writer, "Container Style");
                    HeaderString(writer, "Header Version");
                    HeaderString(writer, "Container Full Name");
                    if(version != 0x00010000) {
                        HeaderString(writer, "");
                        if(version == 0x00020002) {
                            writer.Write(0x50475342u);
                            writer.Write(0u);
                            writer.Write((ushort)0);
                            writer.Write((ushort)0);
                            writer.Write(eudcFlags);
                            writer.Write((uint)(eudc?.Length ?? 0));
                            if(eudc != null)
                                writer.Write(Xor(eudc, eudcFlags));
                        }
                    }
                    writer.Write(Xor(payload, flags));
                }
                byte[] data = output.ToArray();
                Little32(data, 0, (uint)data.Length);
                Little32(data, 4, (uint)payload.Length);
                Little32(data, 8, version);
                Little32(data, 12, flags | (eudc == null ? 0u : 0x20u));
                data[32] = (byte)permissions;
                data[33] = (byte)(permissions >> 8);
                data[34] = 0x4c;
                data[35] = 0x50;
                return data;
            }
        }
        static byte[] Xor(byte[] bytes, uint flags) {
            return (flags & 0x10000000) == 0 ? bytes : bytes.Select(b => (byte)(b ^ 0x50)).ToArray();
        }
        static void HeaderString(BinaryWriter writer, string text) {
            byte[] value = Encoding.Unicode.GetBytes(text);
            writer.Write((ushort)0);
            writer.Write((ushort)value.Length);
            writer.Write(value);
        }
        internal static void Little32(byte[] data, int offset, uint value) {
            for(int i = 0; i < 4; i++)
                data[offset + i] = (byte)(value >> (8 * i));
        }
    }
}
