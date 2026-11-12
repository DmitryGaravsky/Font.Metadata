using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Fonts.Metadata.Internal;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;
using static Fonts.Metadata.Tests.WebFontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class WebFontRecoveryTests {
        [Test]
        public void IndependentMtxVectorsMatchExpectedBytes() {
            byte[] values = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
            byte[] expected = Enumerable.Repeat(values, 4).SelectMany(v => v)
                .Concat(Encoding.ASCII.GetBytes("ABABABABABABABAB" + string.Concat(Enumerable.Repeat("ABCDEF", 30))))
                .Concat(Enumerable.Repeat(values.Reverse(), 2).SelectMany(v => v)).ToArray();
            Assert.That(Decode(EotFontVectors.Commands), Is.EqualTo(expected));
            Assert.That(Decode(EotFontVectors.RunLength), Is.EqualTo(new byte[] { 65, 65, 65, 254, 66 }));
            Assert.That(Decode(EotFontVectors.Metadata), Is.EqualTo(Convert.FromBase64String(EotFontVectors.Sfnt)));
            string hash = Convert.ToHexString(SHA256.HashData(Decode(EotFontVectors.LongCopy)));
            Assert.That(hash, Is.EqualTo("1220AF7265415382AC2998353FC0339B31AA22EF754F1B5EE638729181D4FE73"));
        }
        [Test]
        public void MtxLzAndRleAreChargedTogetherAtExactBoundary() {
            byte[] input = Convert.FromBase64String(EotFontVectors.RunLength);
            var measured = new ReadingContext(new FontReadLimits(), default);
            using(var result = MtxDecoder.Decode(input, measured, new SourceLocation(0, 0), 0)) {
                Assert.That(result.Complete, Is.True);
                Assert.That(measured.DecodedBytes, Is.GreaterThan(result.First!.Length));
            }
            Assert.That(measured.RetainedBytes, Is.Zero);
            for(int delta = -1; delta <= 1; delta++) {
                var limits = new FontReadLimits { MaxMtxPayloadBytes = (int)measured.DecodedBytes + delta };
                var context = new ReadingContext(limits, default);
                using(var result = MtxDecoder.Decode(input, context, new SourceLocation(0, 0), 0)) {
                    Assert.That(result.Complete, Is.EqualTo(delta >= 0));
                    Assert.That(result.Limited, Is.EqualTo(delta < 0));
                }
                Assert.That(context.RetainedBytes, Is.Zero);
            }
        }
        [Test]
        public void MtxAllThreeStreamsConsumeThePayloadBudget() {
            byte[] input = CombineStreams(EotFontVectors.Metadata, EotFontVectors.RunLength, EotFontVectors.RunLength);
            var context = new ReadingContext(new FontReadLimits(), default);
            using(var complete = MtxDecoder.Decode(input, context, new SourceLocation(0, 0), 0)) {
                Assert.That(complete.Complete, Is.True);
                Assert.That(context.DecodedBytes, Is.GreaterThan(complete.First!.Length + 10));
            }
            var options = new FontReadOptions { Limits = new FontReadLimits { MaxMtxPayloadBytes = (int)context.DecodedBytes - 1 } };
            var result = FontMetadataReader.Inspect(Eot(input, flags: 4), options);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].FamilyName, Is.EqualTo("Example Sans"));
            Assert.That(result.Metadata.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(FontDiagnosticCodes.LimitExceeded));
        }
        [Test]
        public void MtxMainAndEudcShareWorkButHaveSeparatePayloadCaps() {
            byte[] mtx = Convert.FromBase64String(EotFontVectors.Metadata);
            int work = Convert.FromBase64String(EotFontVectors.Sfnt).Length;
            byte[] input = Eot(mtx, flags: 4, eudc: mtx, eudcFlags: 4);
            var partial = FontMetadataReader.Inspect(input, new FontReadOptions {
                Limits = new FontReadLimits { MaxTotalDecodedBytes = work * 2 - 1, MaxMtxPayloadBytes = work }
            });
            Assert.That(partial.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(partial.Metadata.Faces[1].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            var complete = FontMetadataReader.Inspect(input, new FontReadOptions {
                Limits = new FontReadLimits { MaxTotalDecodedBytes = work * 2, MaxMtxPayloadBytes = work }
            });
            Assert.That(complete.Status, Is.EqualTo(FontReadStatus.Complete));
        }
        [Test]
        public void FailedSmallMtxMainDoesNotConfiscateTheRemainingBudget() {
            byte[] bad = Convert.FromBase64String(EotFontVectors.InvalidRunLength);
            byte[] good = Convert.FromBase64String(EotFontVectors.Metadata);
            var result = FontMetadataReader.Inspect(Eot(bad, flags: 4, eudc: good, eudcFlags: 4),
                new FontReadOptions { Limits = new FontReadLimits { MaxTotalDecodedBytes = 1000 } });
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(result.Metadata.Faces[1].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Metadata.Faces[1].FamilyName, Is.EqualTo("Example Sans"));
        }
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void MtxSecondaryTruncationAndTrailingDataKeepOnlyCompleteFirstStream(int stream) {
            byte[] input = Convert.FromBase64String(EotFontVectors.Metadata);
            int offset = stream == 0 ? 10 : U24(input, stream == 1 ? 4 : 7);
            input[offset] ^= 0x80;
            var result = FontMetadataReader.Inspect(Eot(input, flags: 4));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            var face = result.Metadata!.Faces[0];
            Assert.That(face.NameTableStatus, Is.EqualTo(stream == 0 ? FontSectionStatus.Invalid : FontSectionStatus.Partial));
            Assert.That(face.NameRecords.Count, stream == 0 ? Is.Zero : Is.GreaterThan(0));
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(FontDiagnosticCodes.InvalidCompressedData));
        }
        [Test]
        public void XorAndMtxWindowAreBothIncludedInRetainedLimit() {
            byte[] input = Eot(Convert.FromBase64String(EotFontVectors.Metadata), flags: 0x10000004);
            var result = FontMetadataReader.Inspect(input,
                new FontReadOptions { Limits = new FontReadLimits { MaxRetainedDecodedBytes = 7200 } });
            Assert.That(result.Metadata!.EotHeaderStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Metadata.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.LimitExceeded));
        }
        [TestCase("woff")]
        [TestCase("woff2")]
        [TestCase("mtx")]
        [TestCase("rle")]
        public void CancellationAfterDecodedProgressPropagatesAndReleasesBuffers(string format) {
            using(var cancellation = new CancellationTokenSource()) {
                ReadingContext? context = null;
                context = new ReadingContext(new FontReadLimits(), cancellation.Token, () => {
                    if(context!.DecodedBytes > (format == "mtx" ? 100000 : 3))
                        cancellation.Cancel();
                    cancellation.Token.ThrowIfCancellationRequested();
                });
                byte[] data = format switch {
                    "woff" => Woff(new Table("name", NameData(new string('A', 200)), true)),
                    "woff2" => Woff2([new Table("name", NameData())]),
                    "rle" => Eot(Convert.FromBase64String(EotFontVectors.RunLength), flags: 4),
                    _ => Eot(Convert.FromBase64String(EotFontVectors.LongCopy), flags: 4)
                };
                byte[] snapshot = (byte[])data.Clone();
                Assert.Throws<OperationCanceledException>(() => ReadInternal(format, data, context));
                Assert.That(context.DecodedBytes, Is.GreaterThan(3));
                Assert.That(context.RetainedBytes, Is.Zero);
                Assert.That(context.Faces, Is.Empty);
                Assert.That(data, Is.EqualTo(snapshot));
            }
        }
        [TestCase("woff2")]
        [TestCase("header")]
        [TestCase("xor")]
        public void CancellationAtTraversalHeaderAndXorCheckpointsIsDeterministic(string phase) {
            byte[] data = phase == "woff2"
                ? Woff2([new Table("name", NameData())], Enumerable.Repeat(new ushort[] { 0 }, 30).ToArray())
                : Eot(Sfnt(("ZZZZ", new byte[50000]), ("name", NameData())), flags: phase == "xor" ? 0x10000000u : 0);
            int calls = 0, retainedCalls = 0;
            ReadingContext? context = null;
            context = new ReadingContext(new FontReadLimits(), default, () => {
                calls++;
                if(context!.RetainedBytes > 0)
                    retainedCalls++;
                bool cancel = phase == "xor" ? retainedCalls == 10 : calls == 12;
                if(cancel)
                    throw new OperationCanceledException();
            });
            Assert.Throws<OperationCanceledException>(() => ReadInternal(phase, data, context));
            Assert.That(calls, Is.GreaterThan(1));
            Assert.That(context.RetainedBytes, Is.Zero);
            Assert.That(context.DecodedBytes, Is.Zero);
        }
        [TestCase("rawWoff")]
        [TestCase("zlib")]
        [TestCase("brotli")]
        [TestCase("rawEot")]
        [TestCase("xor")]
        [TestCase("mtx")]
        [TestCase("eudc")]
        public void DiagnosticCoordinatesKeepRootInputAndLogicalTableOrigins(string encoding) {
            byte[] sfnt = Convert.FromBase64String(EotFontVectors.Sfnt);
            int nameOffset = ReadOffset(sfnt, 36), nameLength = ReadOffset(sfnt, 40);
            byte[] names = sfnt.AsSpan(nameOffset, nameLength).ToArray();
            byte[] mtx = Convert.FromBase64String(EotFontVectors.Metadata);
            byte[] input;
            long anchor;
            bool compressed = encoding is "zlib" or "brotli" or "mtx" or "eudc";
            int face = encoding == "eudc" ? 1 : 0;
            if(encoding is "rawWoff" or "zlib") {
                input = Woff(new Table("name", names, encoding == "zlib"));
                anchor = ReadOffset(input, 48);
            }
            else if(encoding == "brotli") {
                input = Woff2([new Table("name", names)], [[0], [0]]);
                anchor = input.Length - ReadOffset(input, 20);
            }
            else {
                input = encoding == "eudc" ? Eot(sfnt, eudc: mtx, eudcFlags: 4)
                    : Eot(encoding == "mtx" ? mtx : sfnt, flags: encoding == "mtx" ? 4u : encoding == "xor" ? 0x10000000u : 0u);
                anchor = encoding == "eudc" ? input.Length - sfnt.Length - mtx.Length + 10
                    : input.Length - (encoding == "mtx" ? mtx.Length : sfnt.Length) + (encoding == "mtx" ? 10 : nameOffset);
            }
            var options = new FontReadOptions { Limits = new FontReadLimits { MaxNameRecords = 1 } };
            byte[] prefixed = new byte[19].Concat(input).ToArray();
            using(var stream = new MemoryStream(prefixed)) {
                stream.Position = 19;
                var results = new[] { FontMetadataReader.Inspect(input, options),
                    FontMetadataReader.Inspect(prefixed, 19, input.Length, options), FontMetadataReader.Inspect(stream, options) };
                foreach(var result in results) {
                    var diagnostic = result.Diagnostics.Single(d => d.FaceIndex == face && d.TableTag == "name");
                    Assert.That(diagnostic.TableByteOffset, Is.EqualTo(18));
                    Assert.That(diagnostic.ByteOffset, Is.EqualTo(anchor + (compressed ? 0 : 18)));
                    Assert.That(diagnostic.Code, Is.EqualTo(FontDiagnosticCodes.LimitExceeded));
                    Assert.That(diagnostic.Severity, Is.EqualTo(FontDiagnosticSeverity.Error));
                }
                Assert.That(stream.Position, Is.EqualTo(19));
            }
        }
        [Test]
        public void SharedBadNameUsesOneStringBudgetAndLocalizedDiagnostics() {
            byte[] names = Names(Name.Text(13, "MIT"), new Name(14, [0xd8, 0]));
            byte[] input = Woff2([new Table("name", names)], [[0], [0]]);
            var result = FontMetadataReader.Inspect(input,
                new FontReadOptions { Limits = new FontReadLimits { MaxTotalNameStringBytes = 8 } });
            Assert.That(result.Diagnostics.Select(d => d.FaceIndex), Is.EqualTo(new int?[] { 0, 1 }));
            Assert.That(result.Diagnostics.All(d => d.Code == FontDiagnosticCodes.InvalidNameTable), Is.True);
            Assert.That(result.Diagnostics[0].ByteOffset, Is.EqualTo(result.Diagnostics[1].ByteOffset));
            Assert.That(result.Diagnostics[0].TableByteOffset, Is.EqualTo(result.Diagnostics[1].TableByteOffset));
            Assert.That(result.Metadata!.Faces[0].NameRecords, Is.SameAs(result.Metadata.Faces[1].NameRecords));
            foreach(var face in result.Metadata.Faces) {
                var analysis = FontLicenseAnalyzer.Analyze(face);
                Assert.That(analysis.IsSourceComplete, Is.False);
                Assert.That(analysis.Hints[0].SourceRecord, Is.SameAs(face.NameRecords[0]));
            }
        }
        [Test]
        public void Woff2AllUInt255EncodingsOf506AreAccepted() {
            foreach(byte[] encoding in new byte[][] { [255, 253], [254, 0], [253, 1, 250] }) {
                var faces = Enumerable.Repeat(new ushort[] { 0 }, 506).ToArray();
                byte[] input = Woff2([new Table("name", NameData())], faces,
                    value => value == 506 ? encoding : UInt255(value));
                var result = FontMetadataReader.Inspect(input, new FontReadOptions { Limits = new FontReadLimits { MaxFaces = 506 } });
                Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete));
                Assert.That(result.Metadata!.Faces, Has.Count.EqualTo(506));
            }
        }
        static byte[] Decode(string vector) {
            var context = new ReadingContext(new FontReadLimits(), default);
            using(var result = MtxDecoder.Decode(Convert.FromBase64String(vector), context, new SourceLocation(0, 0), 0)) {
                Assert.That(result.Complete, Is.True);
                return result.First!.Content.ToArray();
            }
        }
        static void ReadInternal(string format, byte[] data, ReadingContext context) {
            if(format == "woff")
                WoffParser.Read(data, context);
            else if(format == "woff2")
                Woff2Parser.Read(data, context);
            else
                EotParser.Read(data, context);
        }
        static int U24(byte[] bytes, int offset) {
            return bytes[offset] << 16 | bytes[offset + 1] << 8 | bytes[offset + 2];
        }
        static byte[] CombineStreams(params string[] vectors) {
            var result = new byte[10].ToList();
            result[0] = 3;
            for(int index = 0; index < 3; index++) {
                if(index > 0) {
                    int field = index == 1 ? 4 : 7;
                    result[field] = (byte)(result.Count >> 16);
                    result[field + 1] = (byte)(result.Count >> 8);
                    result[field + 2] = (byte)result.Count;
                }
                byte[] bytes = Convert.FromBase64String(vectors[index]);
                result.AddRange(bytes.AsSpan(10, U24(bytes, 4) - 10).ToArray());
            }
            return result.ToArray();
        }
    }
}
