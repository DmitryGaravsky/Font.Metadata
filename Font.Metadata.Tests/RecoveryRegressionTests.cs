using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using Fonts.Metadata.Internal;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class RecoveryRegressionTests {
        const string AllocationFailureMessage = "Allocated {0} bytes for a {1}-byte input.";
        const string SharedValueAllocationFailureMessage = "Allocated {0} bytes for one shared preferred value.";
        const string AllocationReport = "Shared name input: {0} bytes; allocated: {1} bytes.";
        [TestCase(false)]
        [TestCase(true)]
        public void UnsupportedFaceCannotReserveAnotherFacesMetadata(bool reverseOrder) {
            // A valid directory at 20 and an unsupported header at 48. Its untrusted count
            // would claim bytes 48..124, overlapping the valid name table at 80.
            const int validDirectoryOffset = 20, unsupportedHeaderOffset = 48, validNameOffset = 80;
            const int inputSize = 160, untrustedTableCount = 4;
            byte[] font = new byte[inputSize];
            Encoding.ASCII.GetBytes("ttcf").CopyTo(font, 0);
            U32(font, CollectionVersionOffset, CollectionVersion1);
            U32(font, CollectionFaceCountOffset, 2);
            U32(font, CollectionFacePosition(0), (uint)(reverseOrder ? unsupportedHeaderOffset : validDirectoryOffset));
            U32(font, CollectionFacePosition(1), (uint)(reverseOrder ? validDirectoryOffset : unsupportedHeaderOffset));
            U32(font, validDirectoryOffset, TrueTypeFlavor);
            U16(font, validDirectoryOffset + TableCountOffset, 1);
            Encoding.ASCII.GetBytes("name").CopyTo(font, validDirectoryOffset + SfntHeaderSize);
            byte[] names = Names(Name.Text(FamilyNameId, "Valid"));
            U32(font, validDirectoryOffset + TableOffsetPosition(0), validNameOffset);
            U32(font, validDirectoryOffset + TableLengthPosition(0), (uint)names.Length);
            Encoding.ASCII.GetBytes("BAD!").CopyTo(font, unsupportedHeaderOffset);
            U16(font, unsupportedHeaderOffset + TableCountOffset, untrustedTableCount);
            names.CopyTo(font, validNameOffset);

            var result = FontMetadataReader.Inspect(font);
            int good = reverseOrder ? 1 : 0, bad = 1 - good;
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces.Select(x => x.Index), Is.EqualTo([0, 1]));
            Assert.That(result.Metadata.Faces[good].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Metadata.Faces[good].FamilyName, Is.EqualTo("Valid"));
            Assert.That(result.Metadata.Faces[bad].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.UnsupportedFormat));
            Assert.That(result.Diagnostics.Single().FaceIndex, Is.EqualTo(bad));
            var exception = Assert.Throws<FontReadException>(() => FontMetadataReader.Read(font))!;
            Assert.That(exception.Result.Metadata!.Faces[good].FamilyName, Is.EqualTo("Valid"));
        }

        [TestCase("missing", 1)]
        [TestCase("unicode", 1)]
        [TestCase("budget", 1)]
        [TestCase("format0", 1)]
        [TestCase("missing", 16)]
        public void UnreadableLanguagePreservesRawValueWithoutBecomingPreferred(string failure, int nameId) {
            var records = new[] {
                Name.Text((ushort)nameId, "Broken", language: failure == "missing" ? (ushort)0x8001 : (ushort)0x8000),
                Name.Text(FamilyNameId, "French", language: 0x040c)
            };
            byte[] names = failure == "format0" ? Names(records) :
                NamesWithLanguages(records, ["fr-CA-x-private-long"]);
            if(failure == "unicode") {
                // Two name records, one language record, then the two 12-byte name strings.
                const int firstLanguageStringOffset = 60;
                const ushort unpairedHighSurrogate = 0xd800;
                U16(names, firstLanguageStringOffset, unpairedHighSurrogate);
            }
            var options = failure == "budget" ?
                new FontReadOptions { Limits = new FontReadLimits { MaxTotalNameStringBytes = 24 } } : null;
            var result = FontMetadataReader.Inspect(Sfnt(("name", names)), options);
            var face = result.Metadata!.Faces[0];
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(face.NameTableStatus, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(face.NameRecords.Select(x => x.Value), Is.EqualTo(["Broken", "French"]));
            Assert.That(face.NameRecords.Select(x => x.Index), Is.EqualTo([0, 1]));
            Assert.That(face.NameRecords[0].LanguageTag, Is.Null);
            Assert.That(face.FamilyName, Is.EqualTo("French"));
            Assert.That(result.Diagnostics.Any(x => x.Category == (failure == "budget" ?
                FontDiagnosticCategory.LimitExceeded : FontDiagnosticCategory.MalformedData)), Is.True);
        }

        [Test]
        public void AnUnreadableLanguageWithoutFallbackLeavesPreferredNameNull() {
            var font = Sfnt(("name", NamesWithLanguages([Name.Text(FamilyNameId, "Raw", language: 0x8000)], Array.Empty<string>())));
            var result = FontMetadataReader.Inspect(font);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].NameRecords.Single().Value, Is.EqualTo("Raw"));
            Assert.That(result.Metadata.Faces[0].FamilyName, Is.Null);
        }

        [Test]
        public void BlankHighPriorityNamesDoNotPreventLowerPriorityFallback() {
            var font = Sfnt(("name", Names(Name.Text(FamilyNameId, "  \0 "), Name.Text(FamilyNameId, "French", language: 0x040c))));
            Assert.That(FontMetadataReader.Read(font).Faces[0].FamilyName, Is.EqualTo("French"));
        }

        [Test]
        public void SharedLongNamesDoNotMultiplyStringAllocationsByRecordsAndFaces() {
            const int recordCount = 512, faceCount = 8;
            string raw = " " + new string('A', 16383);
            byte[] names = RepeatedNames(raw, recordCount);
            byte[] font = SharedNames(names, faceCount);
            var options = new FontReadOptions { Limits = new FontReadLimits { MaxTotalNameStringBytes = raw.Length * sizeof(char) } };
            FontMetadataReader.Inspect(Example()); // Keep JIT/encoding initialization out of the measurement.

            long before = GC.GetAllocatedBytesForCurrentThread();
            var result = FontMetadataReader.Inspect(font, options);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete));
            Assert.That(result.Metadata!.Faces.Count, Is.EqualTo(faceCount));
            foreach(var face in result.Metadata.Faces) {
                Assert.That(face.FamilyName, Is.EqualTo(raw.Substring(1)));
                Assert.That(face.NameRecords.Count, Is.EqualTo(recordCount));
                Assert.That(face.NameRecords[0].Value, Is.EqualTo(raw));
            }
            // A generous ceiling for input records and metadata; the regression allocated >128 MiB.
            Assert.That(allocated, Is.LessThan(1024 * 1024), string.Format(AllocationFailureMessage, allocated, font.Length));
            TestContext.Out.WriteLine(AllocationReport, font.Length, allocated);
        }

        [Test]
        public void SharedTablePreferredValuesDoNotAllocateAStringPerFace() {
            byte[] font = SharedNames(Names(Name.Text(FamilyNameId, " " + new string('A', 32766))), 256);
            FontMetadataReader.Inspect(Example());
            long before = GC.GetAllocatedBytesForCurrentThread();
            var result = FontMetadataReader.Inspect(font);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete));
            Assert.That(result.Metadata!.Faces.All(x => x.FamilyName!.Length == 32766), Is.True);
            Assert.That(allocated, Is.LessThan(2 * 1024 * 1024), string.Format(SharedValueAllocationFailureMessage, allocated));
        }

        [Test]
        public void CancellationDuringPreferredSelectionStopsBeforeTheNextRecord() {
            var record = FontMetadataReader.Read(Example()).Faces[0].NameRecords[0];
            using(var cancellation = new CancellationTokenSource()) {
                var records = new CancelDuringSelectionList(record, cancellation);
                var exception = Assert.Throws<OperationCanceledException>(() => NameSelection.Select(records, cancellation.Token))!;
                Assert.That(records.Accesses, Is.EqualTo(2));
                Assert.That(exception.CancellationToken, Is.EqualTo(cancellation.Token));
            }
        }

        [TestCase("00000100")]
        [TestCase("01000200")]
        [TestCase("02000200")]
        public void EotRecognitionRequiresACompleteLittleEndianPrefix(string versionBytes) {
            const int prefixSize = 36, versionOffset = 8, magicOffset = 34;
            var prefix = new byte[prefixSize];
            Convert.FromHexString(versionBytes).CopyTo(prefix, versionOffset);
            prefix[magicOffset] = 0x4c;
            prefix[magicOffset + 1] = 0x50;
            var result = FontMetadataReader.Inspect(prefix);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(result.Metadata!.Format, Is.EqualTo(FontFormat.Eot));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.InvalidEotHeader));
            Assert.That(FontMetadataReader.Inspect(prefix.AsSpan(0, prefixSize - 1)).Status, Is.EqualTo(FontReadStatus.NotRecognized));
            prefix[magicOffset] = 0x50;
            prefix[magicOffset + 1] = 0x4c;
            Assert.That(FontMetadataReader.Inspect(prefix).Status, Is.EqualTo(FontReadStatus.NotRecognized));
        }

        // A deterministic cancellation point inside the real selection loop, without timers,
        // public test hooks, large inputs or races with a worker thread.
        sealed class CancelDuringSelectionList : IReadOnlyList<FontMetadata.NameRecord> {
            readonly FontMetadata.NameRecord record;
            readonly CancellationTokenSource cancellation;
            internal CancelDuringSelectionList(FontMetadata.NameRecord record, CancellationTokenSource cancellation) {
                this.record = record;
                this.cancellation = cancellation;
            }
            internal int Accesses { get; private set; }
            public int Count { get { return 3; } }
            public FontMetadata.NameRecord this[int index] {
                get {
                    Accesses++;
                    if(index == 1)
                        cancellation.Cancel();
                    return record;
                }
            }
            public IEnumerator<FontMetadata.NameRecord> GetEnumerator() {
                for(int i = 0; i < Count; i++)
                    yield return this[i];
            }
            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        static byte[] RepeatedNames(string text, int count) {
            int storage = NameHeaderSize + count * NameRecordSize;
            byte[] value = Encoding.BigEndianUnicode.GetBytes(text);
            byte[] result = new byte[storage + value.Length];
            U16(result, NameCountOffset, count);
            U16(result, NameStorageOffset, storage);
            for(int i = 0; i < count; i++) {
                int pos = NameRecordPosition(i);
                U16(result, pos, WindowsPlatform);
                U16(result, pos + NameEncodingField, WindowsUnicodeBmpEncoding);
                U16(result, pos + NameLanguageField, EnglishLanguage);
                U16(result, pos + NameIdField, FamilyNameId);
                U16(result, pos + NameLengthField, value.Length);
            }
            value.CopyTo(result, storage);
            return result;
        }

        static byte[] SharedNames(byte[] names, int count) {
            // Start with empty tables, then append exactly one payload and point all faces at it.
            byte[] font = Collection(Enumerable.Range(0, count).Select(_ => new[] { ("name", Array.Empty<byte>()) }).ToArray());
            int offset = font.Length;
            Array.Resize(ref font, offset + names.Length);
            names.CopyTo(font, offset);
            for(int i = 0; i < count; i++) {
                int directory = ReadOffset(font, CollectionFacePosition(i));
                U32(font, directory + TableOffsetPosition(0), (uint)offset);
                U32(font, directory + TableLengthPosition(0), (uint)names.Length);
            }
            return font;
        }
    }
}
