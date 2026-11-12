using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;
using static Fonts.Metadata.Tests.WebFontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class WebFontLayoutTests {
        [TestCase(false)]
        [TestCase(true)]
        public void OptionalBlockOverlappingNameDoesNotDiscardIndependentOs2(bool outOfBounds) {
            byte[] data = Woff(new Table("name", NameData()), new Table("OS/2", Os2()));
            U32(data, 24, outOfBounds ? uint.MaxValue - 3 : (uint)ReadOffset(data, 68));
            U32(data, 28, 4);
            U32(data, 32, 4);
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].Os2TableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Metadata.Faces[0].NameTableStatus,
                Is.EqualTo(outOfBounds ? FontSectionStatus.Complete : FontSectionStatus.Invalid));
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(FontDiagnosticCodes.InvalidContainer));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void OptionalPrivateBlockMustEndAtEof(bool woff2) {
            byte[] data = woff2 ? Woff2([new Table("name", NameData())]) : Woff(new Table("name", NameData()));
            int offset = (data.Length + 3) & ~3;
            Array.Resize(ref data, offset + 4);
            U32(data, woff2 ? 40 : 36, (uint)offset);
            U32(data, woff2 ? 44 : 40, 4);
            U32(data, 8, (uint)data.Length);
            Assert.That(FontMetadataReader.Inspect(data).Status, Is.EqualTo(FontReadStatus.Complete));
            Array.Resize(ref data, data.Length + 1);
            U32(data, 8, (uint)data.Length);
            Assert.That(FontMetadataReader.Inspect(data).Status, Is.EqualTo(FontReadStatus.Partial));
        }
        [Test]
        public void WoffReservedIsAnErrorAndSecondZlibMemberIsRejected() {
            byte[] data = Woff(new Table("name", NameData(new string('A', 500)), true));
            U16(data, 14, 1);
            Assert.That(FontMetadataReader.Inspect(data).Status, Is.EqualTo(FontReadStatus.Partial));
            U16(data, 14, 0);
            int start = ReadOffset(data, 48), length = ReadOffset(data, 52);
            byte[] second = Compress([1], false);
            data = data.Take(start + length).Concat(second).ToArray();
            int stored = length + second.Length;
            Array.Resize(ref data, (data.Length + 3) & ~3);
            U32(data, 52, (uint)stored);
            U32(data, 8, (uint)data.Length);
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.InvalidCompressedData));
        }
        [Test]
        public void BrotliTruncatedAfterOs2DoesNotParseZeroFilledNameBuffer() {
            byte[] os2 = Os2(), name = NameData(new string('A', 600));
            byte[] data = Woff2([new Table("OS/2", os2), new Table("name", name)]);
            int start = data.Length - ReadOffset(data, 20);
            using(var output = new MemoryStream()) {
                int completeFirstBlock;
                using(var compressor = new BrotliStream(output, CompressionLevel.Optimal, true)) {
                    compressor.Write(os2);
                    compressor.Flush();
                    completeFirstBlock = (int)output.Length;
                    compressor.Write(name);
                }
                byte[] truncated = output.ToArray().AsSpan(0, completeFirstBlock).ToArray();
                data = data.Take(start).Concat(truncated).ToArray();
                U32(data, 8, (uint)data.Length);
                U32(data, 20, (uint)truncated.Length);
            }
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(result.Metadata.Faces[0].NameRecords, Is.Empty);
            Assert.That(result.Metadata.Faces[0].Os2TableStatus, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(result.Metadata.Faces[0].VendorId, Is.EqualTo("TEST"));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.InvalidCompressedData));
            Assert.That(result.Diagnostics[0].FaceIndex, Is.Null);
            Assert.That(result.Diagnostics[0].TableTag, Is.Null);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void EotRootChecksumAndSignatureFramingAreIndependentOfPayload(bool corruptChecksum) {
            byte[] sfnt = Sfnt(("name", NameData()));
            byte[] data = Eot(sfnt);
            int main = data.Length - sfnt.Length, checksum = main - 20, signatureLength = main - 10;
            if(corruptChecksum)
                data[checksum] ^= 1;
            else {
                data[signatureLength] = 3;
                data = data.Take(signatureLength + 2).Concat(new byte[] { 1, 2, 3 }).Concat(data.Skip(signatureLength + 2)).ToArray();
                Little32(data, 0, (uint)data.Length);
            }
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(corruptChecksum ? FontReadStatus.Partial : FontReadStatus.Complete));
            Assert.That(result.Metadata!.EotHeaderStatus, Is.EqualTo(corruptChecksum ? FontSectionStatus.Partial : FontSectionStatus.Complete));
            Assert.That(result.Metadata.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
        }
        [Test]
        public void EotVersion2EudcFlagDoesNotRequireVersion3Fields() {
            byte[] data = Eot(Sfnt(("name", NameData())), version: 0x00020001, flags: 0x20);
            var result = FontMetadataReader.Read(data);
            Assert.That(result.EotHeaderStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Eot!.EudcFontDataSize, Is.Null);
            Assert.That(result.Faces, Has.Count.EqualTo(1));
        }
        [Test]
        public void EotUnknownMtxVersionAndNestedCollectionAreUnsupportedPayloads() {
            byte[] mtx = Convert.FromBase64String(EotFontVectors.Metadata);
            mtx[0] = 4;
            byte[] collection = Encoding.ASCII.GetBytes("ttcf").Concat(new byte[20]).ToArray();
            foreach(var input in new[] { Eot(mtx, flags: 4), Eot(collection) }) {
                var result = FontMetadataReader.Inspect(input);
                Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
                Assert.That(result.Metadata!.EotHeaderStatus, Is.EqualTo(FontSectionStatus.Complete));
                Assert.That(result.Metadata.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Unsupported));
                Assert.That(result.Diagnostics.Single().Category, Is.EqualTo(FontDiagnosticCategory.UnsupportedFeature));
            }
        }
        [Test]
        public void EmptyLocaIsAcceptedForMetadataInSfntAndCtf() {
            byte[] sfnt = Sfnt(("loca", Array.Empty<byte>()), ("name", NameData()));
            Assert.That(FontMetadataReader.Read(sfnt).Faces[0].FamilyName, Is.EqualTo("Example Sans"));
            Assert.That(FontMetadataReader.Read(Eot(sfnt)).Faces[0].FamilyName, Is.EqualTo("Example Sans"));
            Assert.That(FontMetadataReader.Read(EotMtxFixtures.Create(false)).Faces[0].FamilyName, Is.Not.Null);
        }
        [Test]
        public void RatioUsesLongArithmeticAndHasAnExactBoundary() {
            byte[] name = NameData(new string('A', 2000));
            byte[] data = Woff(new Table("name", name, true));
            int compressed = ReadOffset(data, 52);
            int required = (name.Length + compressed - 1) / compressed;
            var valid = new FontReadOptions { Limits = new FontReadLimits { MaxExpansionRatio = required } };
            Assert.That(FontMetadataReader.Inspect(data, valid).Status, Is.EqualTo(FontReadStatus.Complete));
            var limited = new FontReadOptions { Limits = new FontReadLimits { MaxExpansionRatio = required - 1 } };
            Assert.That(FontMetadataReader.Inspect(data, limited).Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            var huge = new FontReadOptions { Limits = new FontReadLimits { MaxExpansionRatio = int.MaxValue } };
            Assert.That(FontMetadataReader.Inspect(data, huge).Status, Is.EqualTo(FontReadStatus.Complete));
        }
    }
}
