using System;
using System.IO;
using System.Linq;
using System.Threading;
using Fonts.Metadata.Internal;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;
using static Fonts.Metadata.Tests.WebFontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class WebFontBoundaryTests {
        [TestCase("MaxTotalDecodedBytes")]
        [TestCase("MaxMtxPayloadBytes")]
        [TestCase("MaxExpansionRatio")]
        [TestCase("MaxWoff2Tables")]
        [TestCase("MaxWoff2TableReferences")]
        [TestCase("MaxRetainedDecodedBytes")]
        public void NewLimitsRequirePositiveValues(string property) {
            foreach(int value in new[] { 0, -1 }) {
                var limits = new FontReadLimits();
                typeof(FontReadLimits).GetProperty(property)!.SetValue(limits, value);
                var options = new FontReadOptions { Limits = limits };
                Assert.Throws<ArgumentOutOfRangeException>(() => FontMetadataReader.Inspect(new byte[0], options));
            }
        }
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void DecodedBudgetHasAnExactBoundary(int delta) {
            byte[] names = NameData(new string('A', 300));
            foreach(byte[] data in new[] {
                Woff(new Table("name", names, true)),
                Woff2([new Table("name", names)])
            }) {
                var result = Inspect(data, new FontReadLimits { MaxTotalDecodedBytes = names.Length + delta - 1 });
                Assert.That(result.Status, Is.EqualTo(delta == 0 ? FontReadStatus.Failed : FontReadStatus.Complete));
                if(delta == 0)
                    Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            }
        }
        [Test]
        public void WoffFailedWorkIsNotRefundedBeforeTheNextTable() {
            byte[] names = NameData(new string('A', 300));
            byte[] data = Woff(new Table("name", names, true), new Table("OS/2", Os2(), true));
            int nameStart = ReadOffset(data, 68), nameLength = ReadOffset(data, 72);
            data[nameStart + nameLength - 1] ^= 1;
            var result = Inspect(data, new FontReadLimits { MaxTotalDecodedBytes = names.Length });
            Assert.That(result.Metadata!.Faces[0].Os2TableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            Assert.That(result.Metadata.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(FontDiagnosticCodes.LimitExceeded));
        }
        [Test]
        public void Woff2DiscardedBytesConsumeWorkButNotRetainedCapacity() {
            byte[] names = NameData();
            byte[] data = Woff2([new Table("name", names), new Table("ZZZZ", new byte[20000])]);
            var valid = Inspect(data, new FontReadLimits { MaxRetainedDecodedBytes = names.Length });
            Assert.That(valid.Status, Is.EqualTo(FontReadStatus.Complete));
            var limited = Inspect(data, new FontReadLimits { MaxTotalDecodedBytes = names.Length });
            Assert.That(limited.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(limited.Metadata!.Faces[0].NameRecords, Is.Empty);
            var retained = Inspect(data, new FontReadLimits { MaxRetainedDecodedBytes = names.Length - 1 });
            Assert.That(retained.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
        }
        [TestCase(0)]
        [TestCase(1)]
        public void Woff2PerFaceAndFaceLimitsStillTraverseDescriptors(int mode) {
            var tables = new[] { new Table("name", NameData()), new Table("OS/2", Os2()) };
            byte[] data = Woff2(tables, [[0], [0, 1]]);
            var limits = mode == 0 ? new FontReadLimits { MaxFaces = 1 } : new FontReadLimits { MaxTablesPerFace = 1 };
            var result = Inspect(data, limits);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Metadata.Faces.Count, Is.EqualTo(mode == 0 ? 1 : 2));
            if(mode == 1)
                Assert.That(result.Metadata.Faces[1].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            var exhausted = Inspect(data, new FontReadLimits { MaxFaces = 1, MaxWoff2TableReferences = 2 });
            Assert.That(exhausted.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(exhausted.Metadata!.Faces[0].NameRecords, Is.Empty);
        }
        [Test]
        public void Woff2DictionaryAndReferenceBudgetsAreIndependent() {
            byte[] data = Woff2([new Table("name", NameData()), new Table("OS/2", Os2())], [[0, 1], [0, 1]]);
            Assert.That(Inspect(data, new FontReadLimits { MaxWoff2Tables = 1 }).Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(Inspect(data, new FontReadLimits { MaxWoff2Tables = 2, MaxWoff2TableReferences = 4 }).Status,
                Is.EqualTo(FontReadStatus.Complete));
        }
        [Test]
        public void Woff2AcceptsNonMinimalUInt255AndKnownTagEncoding() {
            byte[] data = Woff2([new Table("name", NameData())], [[0], [0]],
                value => [253, (byte)(value >> 8), (byte)value]);
            Assert.That(FontMetadataReader.Inspect(data).Status, Is.EqualTo(FontReadStatus.Complete));
            data = Woff2([new Table("name", NameData())]);
            data = data.Take(48).Concat(new byte[] { 5 }).Concat(data.Skip(53)).ToArray();
            U32(data, 8, (uint)data.Length);
            Assert.That(FontMetadataReader.Read(data).Faces[0].FamilyName, Is.EqualTo("Example Sans"));
        }
        [TestCase("8001")]
        [TestCase("FFFFFFFFFF")]
        [TestCase("9080808000")]
        [TestCase("818080808000")]
        public void Woff2RejectsMalformedBase128BeforeDecoding(string encoding) {
            byte[] data = Woff2([new Table("name", NameData())]);
            data = data.Take(53).Concat(Convert.FromHexString(encoding)).Concat(data.Skip(54)).ToArray();
            U32(data, 8, (uint)data.Length);
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(FontDiagnosticCodes.InvalidDirectory));
        }
        [Test]
        public void Woff2LocalDuplicateAndInvalidReferencePreserveSibling() {
            foreach(ushort[] bad in new ushort[][] { [0, 0], [0, 8] }) {
                byte[] data = Woff2([new Table("name", NameData())], [bad, [0]]);
                var result = FontMetadataReader.Inspect(data);
                Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
                Assert.That(result.Metadata!.Faces[1].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
                Assert.That(result.Metadata.Faces[1].Index, Is.EqualTo(1));
            }
        }
        [TestCase(1, FontDiagnosticCodes.UnsupportedTransform)]
        [TestCase(2, FontDiagnosticCodes.UnsupportedTransform)]
        public void Woff2UnsupportedMetadataTransformIsNotMalformed(int transform, string code) {
            byte[] data = Woff2([new Table("name", NameData(), Transform: transform), new Table("OS/2", Os2())]);
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Unsupported));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(code));
            Assert.That(result.Diagnostics[0].Category, Is.EqualTo(FontDiagnosticCategory.UnsupportedFeature));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void Woff2ValidTransformsDoNotRequireOutlineReconstruction(bool transformed) {
            int transform = transformed ? 0 : 3;
            byte[] loca = transformed ? [] : new byte[4];
            byte[] data = Woff2([new Table("glyf", new byte[4], Transform: transform),
                new Table("loca", loca, Transform: transform, Original: 4), new Table("name", NameData())]);
            Assert.That(FontMetadataReader.Inspect(data).Status, Is.EqualTo(FontReadStatus.Complete));
            data = Woff2([new Table("glyf", new byte[4], Transform: transform), new Table("name", NameData())]);
            var broken = FontMetadataReader.Inspect(data);
            Assert.That(broken.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(broken.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(broken.Diagnostics.Select(d => d.Code), Does.Contain(FontDiagnosticCodes.InvalidDirectory));
        }
        [Test]
        public void EotStringsKeepOriginalValuesAndNullDiffersFromEmpty() {
            byte[] data = Eot(Sfnt(("name", NameData())), family: " \0 ");
            var header = FontMetadataReader.Read(data).Eot!;
            Assert.That(header.FamilyName, Is.EqualTo(" \0 "));
            Assert.That(header.EudcCodePage, Is.Zero);
            Assert.That(header.EudcFlags, Is.Zero);
            Assert.That(header.EudcFontDataSize, Is.Zero);
            data = Eot(Sfnt(("name", NameData())), version: 0x00010000, family: "");
            header = FontMetadataReader.Read(data).Eot!;
            Assert.That(header.FamilyName, Is.Empty);
            Assert.That(header.EudcFlags, Is.Null);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void EotBadStringRecoversOnlyThroughKnownFraming(bool badLength) {
            byte[] data = Eot(Sfnt(("name", NameData())));
            if(badLength) {
                data[82] = 0xff;
                data[83] = 0xff;
            }
            else {
                data[84] = 0;
                data[85] = 0xd8;
            }
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.EotHeaderStatus, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(result.Metadata.Eot!.FamilyName, Is.Null);
            Assert.That(result.Metadata.Eot.StyleName, badLength ? Is.Null : Is.EqualTo("Container Style"));
            Assert.That(result.Metadata.Faces[0].FamilyName, Is.EqualTo("Example Sans"));
        }
        [TestCase(2u)]
        [TestCase(0x80000000u)]
        public void EotUnknownFlagsPreserveCompleteHeaderAndIndependentEudc(uint flags) {
            byte[] sfnt = Sfnt(("name", NameData()));
            var result = FontMetadataReader.Inspect(Eot(sfnt, flags: flags, eudc: sfnt));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.EotHeaderStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Metadata.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Unsupported));
            Assert.That(result.Metadata.Faces[1].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Diagnostics.Single().Category, Is.EqualTo(FontDiagnosticCategory.UnsupportedFeature));
        }
        [Test]
        public void EotMaxFacesDoesNotReuseInvalidMainSlot() {
            byte[] data = Eot(new byte[4], eudc: Sfnt(("name", NameData())));
            var result = Inspect(data, new FontReadLimits { MaxFaces = 1 });
            Assert.That(result.Metadata!.Faces, Has.Count.EqualTo(1));
            Assert.That(result.Metadata.Faces[0].Source, Is.EqualTo(FontFaceSource.EotMain));
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(FontDiagnosticCodes.LimitExceeded));
        }
        [Test]
        public void EotSourceIdentityPreventsSameLocalRangeCacheCollisions() {
            byte[] main = Sfnt(("name", NameData("Main Font", "MIT")));
            byte[] eudc = Sfnt(("name", NameData("Eudc Font", "ISC")));
            foreach(uint flags in new uint[] { 0, 0x10000000 }) {
                var result = FontMetadataReader.Read(Eot(main, flags: flags, eudc: eudc, eudcFlags: flags));
                Assert.That(result.Faces[0].FamilyName, Is.EqualTo("Main Font"));
                Assert.That(result.Faces[1].FamilyName, Is.EqualTo("Eudc Font"));
                Assert.That(FontLicenseAnalyzer.Analyze(result.Faces[0]).Hints[0].Identifier, Is.EqualTo("MIT"));
                Assert.That(result.Faces[1].NameRecords.Single(r => r.NameId == 13).Value, Is.EqualTo("ISC"));
            }
        }
        [Test]
        public void EotHeaderAndPayloadShareStringBudget() {
            byte[] data = Eot(Sfnt(("name", NameData())));
            int headerBytes = 2 * ("Container Family".Length + "Container Style".Length + "Header Version".Length + "Container Full Name".Length);
            var result = Inspect(data, new FontReadLimits { MaxTotalNameStringBytes = headerBytes });
            Assert.That(result.Metadata!.EotHeaderStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Metadata.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            var partial = Inspect(data, new FontReadLimits { MaxNameStringBytes = 32 });
            Assert.That(partial.Metadata!.Eot!.FamilyName, Is.EqualTo("Container Family"));
            Assert.That(partial.Metadata.Eot.StyleName, Is.Null);
            Assert.That(partial.Metadata.EotHeaderStatus, Is.EqualTo(FontSectionStatus.Partial));
        }
        [Test]
        public void SuppressedContainerFailuresStillPreventComplete() {
            byte[] data = Woff2([new Table("name", NameData())]);
            U16(data, 14, 1);
            U32(data, 8, 0);
            var result = Inspect(data, new FontReadLimits { MaxDiagnostics = 1 });
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.DiagnosticsTruncated));
        }
        [TestCase("woff")]
        [TestCase("woff2")]
        [TestCase("eot")]
        [TestCase("mtx")]
        public void TruncationAndDeterministicMutationsStayBoundedAndLeaveInputUnchanged(string format) {
            byte[] input = format switch {
                "woff" => Woff(new Table("name", NameData(), true), new Table("OS/2", Os2())),
                "woff2" => Woff2([new Table("name", NameData()), new Table("OS/2", Os2())], [[0, 1], [0]]),
                "eot" => Eot(Sfnt(("name", NameData()))),
                _ => Eot(Convert.FromBase64String(EotFontVectors.Metadata), flags: 4)
            };
            var limits = new FontReadLimits {
                MaxTotalDecodedBytes = 65536,
                MaxRetainedDecodedBytes = 65536,
                MaxWoff2Tables = 100,
                MaxWoff2TableReferences = 1000,
                MaxTablesPerFace = 100
            };
            for(int length = 0; length < input.Length; length++) {
                var result = Inspect(input.AsSpan(0, length).ToArray(), limits);
                Assert.That(result.Status, Is.Not.EqualTo(FontReadStatus.Complete), $"Truncation {length}");
            }
            for(int index = 0; index < input.Length; index++) {
                byte[] mutated = (byte[])input.Clone();
                mutated[index] ^= 0xff;
                byte[] snapshot = (byte[])mutated.Clone();
                var result = Inspect(mutated, limits);
                Assert.That(mutated, Is.EqualTo(snapshot), $"Mutation {index}");
                if(result.Status == FontReadStatus.Complete)
                    Assert.That(result.Diagnostics.All(d => d.Severity != FontDiagnosticSeverity.Error), Is.True);
            }
        }
        [Test]
        public void UnknownOutlineTransformDoesNotCreateASpuriousMalformedPair() {
            byte[] input = Woff2([new Table("glyf", new byte[4], Transform: 1),
                new Table("loca", Array.Empty<byte>(), Transform: 0, Original: 4), new Table("name", NameData())]);
            var result = FontMetadataReader.Inspect(input);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.UnsupportedTransform));
            Assert.That(result.Diagnostics[0].Category, Is.EqualTo(FontDiagnosticCategory.UnsupportedFeature));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
        }
        [Test]
        public void DiscardedBrotliOutlineDoesNotAllocateAWholeDecodedFont() {
            byte[] names = NameData();
            byte[] input = Woff2([new Table("ZZZZ", new byte[4 * 1024 * 1024]), new Table("name", names)]);
            var limits = new FontReadLimits { MaxRetainedDecodedBytes = names.Length, MaxExpansionRatio = int.MaxValue };
            Assert.That(Inspect(input, limits).Status, Is.EqualTo(FontReadStatus.Complete));
            long before = GC.GetAllocatedBytesForCurrentThread();
            var result = Inspect(input, limits);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete));
            Assert.That(result.Metadata!.Faces[0].FamilyName, Is.EqualTo("Example Sans"));
            Assert.That(allocated, Is.LessThan(1024 * 1024));
        }
        static FontInspectionResult Inspect(byte[] bytes, FontReadLimits limits) {
            return FontMetadataReader.Inspect(bytes, new FontReadOptions { Limits = limits });
        }
    }
}
