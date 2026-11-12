using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;
using static Fonts.Metadata.Tests.WebFontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class WebFontReadingTests {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void WoffReadsIndependentCompressedAndRawTables(bool compressName, bool compressOs2) {
            byte[] name = NameData(new string('A', 200));
            byte[] data = Woff(new Table("name", name, compressName), new Table("OS/2", Os2(), compressOs2));
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete), Diagnostics(result));
            AssertEvidence(result.Metadata!.Faces.Single());
            Assert.That(result.Metadata.EotHeaderStatus, Is.EqualTo(FontSectionStatus.Missing));
            Assert.That(result.Metadata.Faces[0].FamilyName, Is.EqualTo(new string('A', 200)));
        }
        [TestCase("checksum")]
        [TestCase("trailer")]
        [TestCase("trailing")]
        [TestCase("shortOutput")]
        [TestCase("longOutput")]
        public void WoffRejectsBadCompressedTableWithoutDiscardingOs2(string mutation) {
            byte[] data = Woff(new Table("name", NameData(new string('A', 200)), true), new Table("OS/2", Os2()));
            const int record = 64;
            int offset = ReadOffset(data, record + 4), length = ReadOffset(data, record + 8);
            if(mutation == "checksum")
                data[offset + length - 1] ^= 1;
            else if(mutation == "trailer")
                U32(data, record + 8, (uint)(length - 1));
            else if(mutation == "trailing") {
                data = data.Concat(new byte[4]).ToArray();
                U32(data, 8, (uint)data.Length);
                U32(data, record + 8, (uint)(length + 1));
            }
            else {
                int original = ReadOffset(data, record + 12) + (mutation == "shortOutput" ? 1 : -1);
                U32(data, record + 12, (uint)original);
            }
            var result = FontMetadataReader.Inspect(data);
            var face = result.Metadata!.Faces.Single();
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial), Diagnostics(result));
            Assert.That(face.NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(face.NameRecords, Is.Empty);
            Assert.That(face.Os2TableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(face.VendorId, Is.EqualTo("TEST"));
            AssertCode(result, FontDiagnosticCodes.InvalidCompressedData);
            Assert.Throws<FontReadException>(() => FontMetadataReader.Read(data));
        }
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Woff2SingleAndCollectionsReuseSharedEvidence(int faceCount) {
            var tables = new[] { new Table("name", NameData()), new Table("OS/2", Os2()) };
            ushort[][]? faces = faceCount == 1 ? null : Enumerable.Range(0, faceCount).Select(_ => new ushort[] { 0, 1 }).ToArray();
            byte[] data = Woff2(tables, faces);
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete), Diagnostics(result));
            Assert.That(result.Metadata!.Faces, Has.Count.EqualTo(faceCount));
            foreach(var face in result.Metadata.Faces)
                AssertEvidence(face);
            if(faceCount > 1)
                Assert.That(result.Metadata.Faces[0].NameRecords, Is.SameAs(result.Metadata.Faces[1].NameRecords));
        }
        [Test]
        public void Woff2ReservedAndReferenceSizeDoNotRejectValidMetadata() {
            byte[] data = Woff2([new Table("name", NameData())]);
            U16(data, 14, 1);
            U32(data, 16, uint.MaxValue);
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete), Diagnostics(result));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.NonConformantContainer));
            Assert.That(result.Diagnostics.Single().Severity, Is.EqualTo(FontDiagnosticSeverity.Warning));
        }
        [Test]
        public void Woff2TrailingCompressedInputSalvagesWholeTablesAsPartial() {
            byte[] data = Woff2([new Table("name", NameData()), new Table("OS/2", Os2())]);
            int length = ReadOffset(data, 20);
            data = data.Concat(new byte[] { 123 }).ToArray();
            U32(data, 8, (uint)data.Length);
            U32(data, 20, (uint)(length + 1));
            var result = FontMetadataReader.Inspect(data);
            var face = result.Metadata!.Faces.Single();
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial), Diagnostics(result));
            Assert.That(face.NameTableStatus, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(face.Os2TableStatus, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(face.FamilyName, Is.EqualTo("Example Sans"));
            var analysis = FontLicenseAnalyzer.Analyze(face);
            Assert.That(analysis.IsSourceComplete, Is.False);
            Assert.That(analysis.Hints.Single().SourceRecord, Is.SameAs(face.NameRecords[1]));
            AssertCode(result, FontDiagnosticCodes.InvalidCompressedData);
        }
        [Test]
        public void Woff2WithoutMetadataStillValidatesCompressedStream() {
            byte[] data = Woff2([new Table("ZZZZ", new byte[256])]);
            Assert.That(FontMetadataReader.Read(data).Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Missing));
            data[^1] ^= 255;
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial), Diagnostics(result));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Missing));
            AssertCode(result, FontDiagnosticCodes.InvalidCompressedData);
        }
        [TestCase(0x00010000u, false, false)]
        [TestCase(0x00010000u, true, true)]
        [TestCase(0x00020001u, false, true)]
        [TestCase(0x00020001u, true, false)]
        [TestCase(0x00020002u, false, false)]
        [TestCase(0x00020002u, true, true)]
        public void EotVersionsSeparateHeaderAndPayload(uint version, bool compressed, bool xor) {
            byte[] payload = Convert.FromBase64String(compressed ? EotFontVectors.Metadata : EotFontVectors.Sfnt);
            uint flags = (compressed ? 4u : 0) | (xor ? 0x10000000u : 0);
            byte[] data = Eot(payload, version, flags);
            byte[] original = data.ToArray();
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete), Diagnostics(result));
            var font = result.Metadata!;
            Assert.That(font.EotHeaderStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(font.Eot!.FamilyName, Is.EqualTo("Container Family"));
            Assert.That(font.Eot.FormatVersion, Is.EqualTo(version));
            Assert.That(font.Eot.Version, Is.EqualTo("Header Version"));
            Assert.That(font.Eot.EmbeddingPermissions, Is.EqualTo(2));
            Assert.That(font.Faces[0].FamilyName, Is.EqualTo("Example Sans"));
            Assert.That(font.Faces[0].Source, Is.EqualTo(FontFaceSource.EotMain));
            Assert.That(font.Faces[0].NameRecords.Any(r => r.Value == "Container Family"), Is.False);
            Assert.That(data, Is.EqualTo(original));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void IndependentWindowsMtxAndRleFixturesAreReadable(bool rle) {
            byte[] data = EotMtxFixtures.Create(rle);
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete), Diagnostics(result));
            Assert.That(result.Metadata!.Faces.Single().FamilyName, Is.Not.Null);
            Assert.That(result.Metadata.EotHeaderStatus, Is.EqualTo(FontSectionStatus.Complete));
        }
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void EotPayloadsUseTheirOwnProcessingFlags(bool mainCompressed, bool eudcCompressed) {
            byte[] main = Convert.FromBase64String(mainCompressed ? EotFontVectors.Metadata : EotFontVectors.Sfnt);
            byte[] eudc = Convert.FromBase64String(eudcCompressed ? EotFontVectors.Metadata : EotFontVectors.Sfnt);
            byte[] data = Eot(main, flags: mainCompressed ? 0x10000004u : 0, eudc: eudc, eudcFlags: eudcCompressed ? 4u : 0x10000000u);
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete), Diagnostics(result));
            Assert.That(result.Metadata!.Faces.Select(f => (f.Index, f.Source)), Is.EqualTo(new[] {
                (0, FontFaceSource.EotMain), (1, FontFaceSource.EotEudc) }));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void EotDamagedPayloadDoesNotEraseOtherPayload(bool damagedMain) {
            byte[] good = Convert.FromBase64String(EotFontVectors.Sfnt);
            byte[] bad = new byte[5];
            byte[] data = Eot(damagedMain ? bad : good, eudc: damagedMain ? good : bad);
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial), Diagnostics(result));
            var valid = result.Metadata!.Faces.Single(f => f.Index == (damagedMain ? 1 : 0));
            Assert.That(valid.FamilyName, Is.EqualTo("Example Sans"));
            Assert.That(result.Metadata.EotHeaderStatus, Is.EqualTo(FontSectionStatus.Complete));
        }
        [Test]
        public void SecondaryMtxFailurePreservesCompleteFirstStreamAsPartial() {
            byte[] mtx = Convert.FromBase64String(EotFontVectors.Metadata);
            byte[] data = Eot(mtx[..^1], flags: 4);
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial), Diagnostics(result));
            Assert.That(result.Metadata!.Faces[0].FamilyName, Is.EqualTo("Example Sans"));
            Assert.That(result.Metadata.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Partial));
            AssertCode(result, FontDiagnosticCodes.InvalidCompressedData);
        }
        [TestCase("woff")]
        [TestCase("woff2")]
        [TestCase("eot")]
        public void SliceAndStreamOriginsAndEvidenceRemainStable(string kind) {
            byte[] names = NameData();
            byte[] data = kind switch {
                "woff" => Woff(new Table("name", names)),
                "woff2" => Woff2([new Table("name", names)]),
                _ => Eot(Sfnt(("name", names)))
            };
            byte[] embedded = new byte[17].Concat(data).ToArray();
            var slice = FontMetadataReader.Read(embedded, 17, data.Length);
            using(var stream = new MemoryStream(embedded, false)) {
                stream.Position = 17;
                var fromStream = FontMetadataReader.Read(stream);
                Assert.That(stream.Position, Is.EqualTo(17));
                Assert.That(fromStream.Faces[0].NameRecords.Select(r => r.Value), Is.EqualTo(slice.Faces[0].NameRecords.Select(r => r.Value)));
                var analysis = FontLicenseAnalyzer.Analyze(fromStream.Faces[0]);
                Assert.That(analysis.Hints.Single().Identifier, Is.EqualTo("MIT"));
            }
        }
        internal static void AssertEvidence(FontMetadata.Face face) {
            Assert.That(face.NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(face.Os2TableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(face.VendorId, Is.EqualTo("TEST"));
            var analysis = FontLicenseAnalyzer.Analyze(face);
            Assert.That(analysis.Hints.Single().Identifier, Is.EqualTo("MIT"));
            Assert.That(analysis.Hints.Single().SourceRecord, Is.SameAs(face.NameRecords[1]));
        }
        internal static void AssertCode(FontInspectionResult result, string code) {
            Assert.That(result.Diagnostics.Any(d => d.Code == code), Is.True, Diagnostics(result));
        }
        internal static string Diagnostics(FontInspectionResult result) {
            return string.Join("; ", result.Diagnostics.Select(d => d.Code + ": " + d.Message + " @" + d.ByteOffset));
        }
    }
}
