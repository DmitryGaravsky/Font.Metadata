using System;
using System.Linq;
using System.Text;
using Fonts.Metadata.Internal;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;
using static Fonts.Metadata.Tests.WebFontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class WebFontReviewTests {
        [Test]
        public void MissingCompressedBytesCannotIncreaseTheWoff2ExpansionBudget() {
            byte[] input = Woff2([new Table("name", NameData()), new Table("ZZZZ", new byte[65536])]);
            U32(input, 20, 65536);
            var context = new ReadingContext(new FontReadLimits { MaxExpansionRatio = 10 }, default);
            Woff2Parser.Read(input, context);
            Assert.That(context.DecodedBytes, Is.Zero);
            Assert.That(context.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            Assert.That(context.Faces[0].NameRecords, Is.Empty);
            Assert.That(context.Diagnostics.Freeze().Select(d => d.Code), Does.Contain(FontDiagnosticCodes.LimitExceeded));
            Assert.That(context.RetainedBytes, Is.Zero);
        }
        [TestCase(28)]
        [TestCase(40)]
        public void UnrelatedOptionalBlockFailureDoesNotInvalidateTheBrotliSource(int offsetField) {
            byte[] input = Woff2([new Table("name", NameData()), new Table("OS/2", Os2())]);
            U32(input, offsetField, (uint)(input.Length + 4));
            U32(input, offsetField + 4, 4);
            if(offsetField == 28)
                U32(input, 36, 4);
            var result = FontMetadataReader.Inspect(input);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Metadata.Faces[0].Os2TableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(FontLicenseAnalyzer.Analyze(result.Metadata.Faces[0]).IsSourceComplete, Is.True);
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(FontDiagnosticCodes.InvalidContainer));
            Assert.Throws<FontReadException>(() => FontMetadataReader.Read(input));
        }
        [Test]
        public void Woff2DoesNotDecodeBytesClaimedByABoundedOptionalBlock() {
            byte[] input = Woff2([new Table("name", NameData(new string('A', 300)))]);
            int start = input.Length - ReadOffset(input, 20);
            int privateStart = (start + 3) & ~3;
            U32(input, 40, (uint)privateStart);
            U32(input, 44, (uint)(input.Length - privateStart));
            var result = FontMetadataReader.Inspect(input);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(result.Metadata!.Faces[0].NameRecords, Is.Empty);
            Assert.That(result.Metadata.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(FontDiagnosticCodes.InvalidContainer));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void WoffRequiresPaddingAfterItsFinalFontTable(bool retainOnePaddingByte) {
            byte[] input = Woff(new Table("name", NameData("X")));
            int tableEnd = ReadOffset(input, 48) + ReadOffset(input, 52);
            Assert.That(input.Length - tableEnd, Is.EqualTo(2));
            Array.Resize(ref input, tableEnd + (retainOnePaddingByte ? 1 : 0));
            U32(input, 8, (uint)input.Length);
            var result = FontMetadataReader.Inspect(input);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].FamilyName, Is.EqualTo("X"));
            Assert.That(result.Metadata.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(FontDiagnosticCodes.InvalidContainer));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void EotRootStringValidationSharesTheStringBudgetIncludingFailedAttempts(bool malformed) {
            byte[] sfnt = Sfnt(("name", NameData()));
            byte[] input = Eot(sfnt);
            int mainStart = input.Length - sfnt.Length;
            int rootLengthField = mainStart - 22;
            int rootStart = rootLengthField + 2;
            byte[] root = malformed ? [0, 0xd8] : Encoding.Unicode.GetBytes("https://example.test\0");
            input = input.Take(rootStart).Concat(root).Concat(input.Skip(rootStart)).ToArray();
            input[rootLengthField] = (byte)root.Length;
            input[rootLengthField + 1] = (byte)(root.Length >> 8);
            Little32(input, rootStart + root.Length, (uint)root.Sum(b => b) ^ 0x50475342u);
            Little32(input, 0, (uint)input.Length);
            int headerNames = 2 * ("Container Family".Length + "Container Style".Length +
                "Header Version".Length + "Container Full Name".Length);
            var context = new ReadingContext(new FontReadLimits(), default);
            EotParser.Read(input, context);
            Assert.That(context.NameStringBytes, Is.EqualTo(headerNames + root.Length + 2 * ("Example Sans".Length + "MIT".Length)));
            Assert.That(context.EotStatus, Is.EqualTo(malformed ? FontSectionStatus.Partial : FontSectionStatus.Complete));
            Assert.That(context.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
            var result = FontMetadataReader.Inspect(input, new FontReadOptions {
                Limits = new FontReadLimits { MaxTotalNameStringBytes = headerNames + root.Length }
            });
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(FontDiagnosticCodes.LimitExceeded));
        }
        [Test]
        public void Woff2UnknownCollectionVersionHasVersionSpecificDiagnostics() {
            byte[] input = Woff2([new Table("name", NameData())], [[0]]);
            const int collectionVersionOffset = 54;
            U32(input, collectionVersionOffset, 0x00030000);
            var result = FontMetadataReader.Inspect(input);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Unsupported));
            Assert.That(result.Metadata!.Faces, Is.Empty);
            var diagnostic = result.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo(FontDiagnosticCodes.UnsupportedTableVersion));
            Assert.That(diagnostic.Category, Is.EqualTo(FontDiagnosticCategory.UnsupportedFeature));
            Assert.That(diagnostic.ByteOffset, Is.EqualTo(collectionVersionOffset));
            Assert.That(diagnostic.TableByteOffset, Is.Null);
        }
        [Test]
        public void UnsupportedEotPayloadKeepsItsReadableFlavorAndSibling() {
            byte[] unknown = Sfnt(("name", NameData()));
            U32(unknown, 0, 0x12345678);
            byte[] good = Sfnt(("name", NameData()));
            var result = FontMetadataReader.Inspect(Eot(unknown, eudc: good));
            Assert.That(result.Metadata!.Faces[0].SfntFlavor, Is.EqualTo(0x12345678u));
            Assert.That(result.Metadata.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Unsupported));
            Assert.That(result.Metadata.Faces[1].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Metadata.EotHeaderStatus, Is.EqualTo(FontSectionStatus.Complete));
        }
        [Test]
        public void OptionalOverlapAfterACompleteTableStillAllowsPartialRecovery() {
            byte[] os2 = Os2(), names = NameData(new string('A', 1000));
            byte[] input = Woff2([new Table("OS/2", os2), new Table("name", names)]);
            int start = input.Length - ReadOffset(input, 20);
            int privateStart;
            using(var output = new System.IO.MemoryStream()) {
                using(var encoder = new System.IO.Compression.BrotliStream(output,
                    System.IO.Compression.CompressionLevel.Optimal, true)) {
                    encoder.Write(os2);
                    encoder.Flush();
                    privateStart = (start + (int)output.Length + 3) & ~3;
                    encoder.Write(names);
                }
                byte[] compressed = output.ToArray();
                input = input.Take(start).Concat(compressed).ToArray();
                U32(input, 20, (uint)compressed.Length);
                U32(input, 8, (uint)input.Length);
            }
            U32(input, 40, (uint)privateStart);
            U32(input, 44, (uint)(input.Length - privateStart));
            var result = FontMetadataReader.Inspect(input);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].Os2TableStatus, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(result.Metadata.Faces[0].VendorId, Is.EqualTo("TEST"));
            Assert.That(result.Metadata.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(result.Metadata.Faces[0].NameRecords, Is.Empty);
        }
        [TestCase(8191)]
        [TestCase(8192)]
        [TestCase(8193)]
        public void ChunkedCopiesKeepTheFinalPartialChunkAndOwnership(int length) {
            byte[] input = Enumerable.Range(0, length).Select(value => (byte)value).ToArray();
            var context = new ReadingContext(new FontReadLimits(), default);
            using(var buffer = new DecodedBuffer(context, length)) {
                ContainerLayout.Copy(input, buffer.Bytes, context);
                Assert.That(buffer.Bytes, Is.EqualTo(input));
                buffer.Resize(length + 1);
                Assert.That(buffer.Bytes.AsSpan(0, length).ToArray(), Is.EqualTo(input));
                Assert.That(buffer.Bytes[length], Is.Zero);
                Assert.That(context.RetainedBytes, Is.EqualTo(length + 1));
                Assert.That(context.DecodedBytes, Is.Zero);
            }
            Assert.That(context.RetainedBytes, Is.Zero);
        }
        [Test]
        public void ResizeCancellationReleasesOnlyTheUncommittedCapacity() {
            ReadingContext? context = null;
            context = new ReadingContext(new FontReadLimits(), default, () => {
                if(context!.RetainedBytes > 16)
                    throw new OperationCanceledException();
            });
            using(var buffer = new DecodedBuffer(context, 16)) {
                buffer.Bytes[0] = 42;
                Assert.Throws<OperationCanceledException>(() => buffer.Resize(32));
                Assert.That(context.RetainedBytes, Is.EqualTo(16));
                Assert.That(buffer.Bytes.Length, Is.EqualTo(16));
                Assert.That(buffer.Bytes[0], Is.EqualTo(42));
            }
            Assert.That(context.RetainedBytes, Is.Zero);
        }
    }
}
