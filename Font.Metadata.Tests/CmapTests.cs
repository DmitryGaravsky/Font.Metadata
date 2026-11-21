using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Fonts.Metadata.Internal;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class CmapTests {
        const int EncodingRecords = 4, EncodingRecordSize = 8, EncodingSubtableOffset = 4;
        const int Format12Groups = 16, Format12GroupSize = 12;
        const int OneRecordSubtable = EncodingRecords + EncodingRecordSize;
        const int Format4ArrayOffsetWord = OneRecordSubtable + 36;
        static byte[] Format4Vector() {
            // Windows BMP. A..D uses delta -66. E..H uses words 2, 0, 1, 3 and delta -1.
            return Convert.FromHexString("00000001000300010000000C" +
                "000400300000000600040001000200440048FFFF000000410045FFFFFFBEFFFF00010000000400000002000000010003");
        }
        [Test]
        public void Format4AppliesModuloDeltaAndSupportsBothKindsOfMissingGlyph() {
            var coverage = Parse(Format4Vector());
            Assert.That(coverage.Status, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That((coverage.Format, coverage.PlatformId, coverage.EncodingId), Is.EqualTo(((ushort?)4, (ushort?)3, (ushort?)1)));
            Assert.That(Intervals(coverage), Is.EqualTo(new[] { (0x41, 0x41), (0x43, 0x45), (0x48, 0x48) }));
            Assert.That(coverage.Diagnostics, Is.Empty);
        }
        [Test]
        public void Format4ExcludesSurrogatesButKeepsMappedNoncharactersIncludingTheTerminator() {
            byte[] table = Convert.FromHexString("00000001000000030000000C" +
                "0004002000000004000400010000E001FFFF0000D7FEFFFF2FFF000200000000");
            var coverage = Parse(table);
            Assert.That(coverage.Status, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(Intervals(coverage), Is.EqualTo(new[] { (0xD7FE, 0xD7FF), (0xE000, 0xE001), (0xFFFF, 0xFFFF) }));
        }
        [Test]
        public void Format12SkipsOnlyTheFirstZeroMapsSupplementaryAndMergesAdjacentGroups() {
            byte[] table = Cmap((0, 4, Sequential((0, 2, 0), (3, 3, 20), (0xD7FD, 0xE001, 8),
                (0x1F600, 0x1F602, 100), (0x10FFFF, 0x10FFFF, 65535))));
            var coverage = Parse(table);
            Assert.That(coverage.Status, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(Intervals(coverage), Is.EqualTo(new[] { (1, 3), (0xD7FD, 0xD7FF), (0xE000, 0xE001), (0x1F600, 0x1F602), (0x10FFFF, 0x10FFFF) }));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void FullRepertoirePrecedesBmpAndTieBreakUsesWindows(bool reverse) {
            (int Platform, int Encoding, byte[] Data)[] records = [
                (0, 4, Sequential((0x10000, 0x10000, 1))),
                (3, 1, Format4Vector()[OneRecordSubtable..]),
                (3, 10, Sequential((0x10001, 0x10001, 1)))
            ];
            if(reverse)
                Array.Reverse(records);
            var coverage = Parse(Cmap(records));
            Assert.That((coverage.Format, coverage.PlatformId, coverage.EncodingId), Is.EqualTo(((ushort?)12, (ushort?)3, (ushort?)10)));
            Assert.That(Intervals(coverage), Is.EqualTo(new[] { (0x10001, 0x10001) }));
        }
        [Test]
        public void SharedSubtableRetainsTheSelectedEncodingRecord() {
            byte[] table = Cmap((0, 4, Sequential((66, 66, 2))), (3, 10, Sequential((67, 67, 3))));
            const int sharedOffset = EncodingRecords + 2 * EncodingRecordSize;
            U32(table, EncodingRecords + EncodingRecordSize + EncodingSubtableOffset, sharedOffset);
            var coverage = Parse(table);
            Assert.That((coverage.PlatformId, coverage.EncodingId), Is.EqualTo(((ushort?)3, (ushort?)10)));
            Assert.That(Intervals(coverage), Is.EqualTo(new[] { (66, 66) }));
        }
        [TestCase(3, 0, 4)]
        [TestCase(0, 5, 14)]
        [TestCase(0, 6, 13)]
        [TestCase(3, 1, 6)]
        public void UnsupportedMappingsDoNotBecomeEmptySuccess(int platform, int encoding, int format) {
            byte[] subtable = [(byte)(format >> 8), (byte)format];
            var coverage = Parse(Cmap((platform, encoding, subtable)));
            Assert.That(coverage.Status, Is.EqualTo(FontSectionStatus.Unsupported));
            Assert.That(coverage.Format, Is.Null);
            Assert.That(coverage.Ranges, Is.Empty);
            Assert.That(coverage.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.UnsupportedCmapSubtable));
        }
        [Test]
        public void AVerifiedSubtableWithOnlyGlyphZeroHasCompleteEmptyCoverage() {
            var coverage = Parse(Cmap((3, 10, Sequential((65, 65, 0)))));
            Assert.That(coverage.Status, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(coverage.Format, Is.EqualTo(12));
            Assert.That(coverage.Ranges, Is.Empty);
        }
        [TestCase(0xFFFFFFFFu)]
        [TestCase(4u)]
        [TestCase(OneRecordSubtable + 1u)]
        public void CandidateOffsetMustPointOutsideTheDirectoryAndContainAFormatWord(uint offset) {
            byte[] table = Cmap((3, 10, new byte[] { 0, 12 }));
            U32(table, EncodingRecords + EncodingSubtableOffset, offset);
            var coverage = Parse(table);
            Assert.That(coverage.Status, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(coverage.Format, Is.Null);
            Assert.That(coverage.Diagnostics.Single().TableByteOffset, Is.EqualTo(8));
        }
        [TestCase(false, FontSectionStatus.Partial)]
        [TestCase(true, FontSectionStatus.Complete)]
        public void UnreadableOffsetOnlyAffectsCoverageIfThatRecordCouldWin(bool keepFull, FontSectionStatus expected) {
            byte[] table = Cmap((3, 1, Format4Vector()[OneRecordSubtable..]), (3, 10, Sequential((66, 66, 1))));
            int damagedRecord = keepFull ? EncodingRecords : EncodingRecords + EncodingRecordSize;
            U32(table, damagedRecord + EncodingSubtableOffset, uint.MaxValue);
            var coverage = Parse(table);
            Assert.That(coverage.Status, Is.EqualTo(expected));
            Assert.That(coverage.Format, Is.EqualTo(keepFull ? 12 : 4));
            Assert.That(coverage.Diagnostics, Has.Count.EqualTo(keepFull ? 0 : 1));
        }
        [TestCase(67u, 66u, 7u)]
        [TestCase(65u, 67u, 7u)]
        [TestCase(67u, 0x110000u, 1u)]
        [TestCase(67u, 68u, 0xFFFFFFFFu)]
        [TestCase(67u, 68u, 65535u)]
        public void MalformedFormat12GroupPreservesOnlyTheVerifiedPrefix(uint start, uint end, uint glyph) {
            byte[] table = Cmap((3, 10, Sequential((65, 66, 7), (start, end, glyph), (70, 70, 1))));
            var coverage = Parse(table);
            Assert.That(coverage.Status, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(Intervals(coverage), Is.EqualTo(new[] { (65, 66) }));
            var diagnostic = coverage.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo(FontDiagnosticCodes.InvalidCmapTable));
            Assert.That(diagnostic.TableByteOffset, Is.EqualTo(40));
            Assert.That(diagnostic.ByteOffset, Is.EqualTo(140));
        }
        [Test]
        public void InvalidPreferredSubtableDoesNotSubstituteBmpCoverage() {
            byte[] table = Cmap((3, 1, Format4Vector()[OneRecordSubtable..]), (3, 10, Sequential((66, 66, 0x10000))));
            var coverage = Parse(table);
            Assert.That(coverage.Status, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(coverage.Format, Is.EqualTo(12));
            Assert.That(coverage.Ranges, Is.Empty);
        }
        [Test]
        public void Format12BoundsItsGroupCountAndRecoversFullGroupsFromTruncation() {
            byte[] table = Cmap((3, 10, Sequential((65, 66, 7), (68, 70, 8))));
            var truncated = Parse(table[..^1]);
            Assert.That(truncated.Status, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(Intervals(truncated), Is.EqualTo(new[] { (65, 66) }));
            Assert.That(truncated.Diagnostics.Single().TableByteOffset, Is.EqualTo(16));
            U32(table, OneRecordSubtable + 12, uint.MaxValue);
            var overflowing = Parse(table);
            Assert.That(overflowing.Status, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(overflowing.Ranges, Is.Empty);
        }
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(65534)]
        public void Format4RejectsGlyphArrayOffsetsIntoMetadataUnalignedOrOutOfBounds(int offset) {
            byte[] table = Format4Vector();
            U16(table, Format4ArrayOffsetWord, offset);
            var context = new ReadingContext(new FontReadLimits(), CancellationToken.None);
            var coverage = CmapParser.Read(table, new SourceLocation(1, 100, Compressed: true), context);
            Assert.That(coverage.Status, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(Intervals(coverage), Is.EqualTo(new[] { (65, 65), (67, 68) }));
            var diagnostic = coverage.Diagnostics.Single();
            Assert.That(diagnostic.TableByteOffset, Is.EqualTo(Format4ArrayOffsetWord));
            Assert.That(diagnostic.ByteOffset, Is.EqualTo(100));
        }
        [TestCase(2, 15, FontSectionStatus.Invalid)]
        [TestCase(2, 40, FontSectionStatus.Partial)]
        [TestCase(6, 7, FontSectionStatus.Invalid)]
        [TestCase(18, 0xFFFE, FontSectionStatus.Partial)]
        [TestCase(24, 0x44, FontSectionStatus.Partial)]
        public void Format4ValidatesDeclaredLengthSegmentCountAndTerminator(int offset, int value, FontSectionStatus expected) {
            byte[] table = Format4Vector();
            U16(table, OneRecordSubtable + offset, value);
            var coverage = Parse(table);
            Assert.That(coverage.Status, Is.EqualTo(expected));
            Assert.That(coverage.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.InvalidCmapTable));
        }
        [Test]
        public void PrefixTruncationCannotReadPastAnyCmapBoundary() {
            byte[][] tables = [Format4Vector(), Cmap((3, 10, Sequential((65, 66, 7), (68, 70, 8))))];
            foreach(byte[] table in tables) {
                for(int length = 0; length < table.Length; length++) {
                    var coverage = Parse(table[..length]);
                    Assert.That(coverage.Status, Is.AnyOf(FontSectionStatus.Invalid, FontSectionStatus.Partial), $"length {length}");
                }
            }
        }
        [TestCase(1, FontSectionStatus.NotRead)]
        [TestCase(2, FontSectionStatus.Partial)]
        [TestCase(3, FontSectionStatus.Complete)]
        public void WorkBudgetCountsEncodingRecordsAndGroups(int budget, FontSectionStatus status) {
            byte[] table = Cmap((3, 10, Sequential((65, 65, 1), (67, 67, 1))));
            var coverage = Parse(table, new FontReadLimits { MaxTotalCmapWork = budget });
            Assert.That(coverage.Status, Is.EqualTo(status));
            Assert.That(coverage.Ranges, Has.Count.EqualTo(budget - 1));
            if(status != FontSectionStatus.Complete)
                Assert.That(coverage.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.LimitExceeded));
        }
        [Test]
        public void ResultBudgetChargesOnlyCompactRangesAndPreservesTheirVerifiedPrefix() {
            byte[] table = Cmap((3, 10, Sequential((65, 65, 1), (66, 66, 2), (68, 68, 1))));
            var coverage = Parse(table, new FontReadLimits { MaxTotalUnicodeRanges = 1 });
            Assert.That(coverage.Status, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(Intervals(coverage), Is.EqualTo(new[] { (65, 66) }));
            Assert.That(coverage.Diagnostics.Single().Category, Is.EqualTo(FontDiagnosticCategory.LimitExceeded));
        }
        [Test]
        public void CancellationAndWorkBudgetReachIntoTheGlyphArray() {
            byte[] table = Format4Vector();
            var limited = Parse(table, new FontReadLimits { MaxTotalCmapWork = 5 });
            Assert.That(limited.Status, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(Intervals(limited), Is.EqualTo(new[] { (65, 65), (67, 69) }));
            Assert.That(limited.Diagnostics.Single().TableByteOffset, Is.EqualTo(56));
            using(var cancel = new CancellationTokenSource()) {
                int checkpoints = 0;
                var context = new ReadingContext(new FontReadLimits(), cancel.Token, () => {
                    if(++checkpoints == 6)
                        cancel.Cancel();
                    cancel.Token.ThrowIfCancellationRequested();
                });
                Assert.That(() => CmapParser.Read(table, 100, context), Throws.TypeOf<OperationCanceledException>());
            }
        }
        [Test]
        public void ReaderDistinguishesOptOutMissingAndPresentCoverage() {
            byte[] table = Cmap((3, 10, Sequential((65, 65, 1))));
            byte[] font = Sfnt(("cmap", table));
            byte[] withoutCmap = Sfnt(("name", Names(Name.Text(FamilyNameId, "No cmap"))));
            var options = new FontReadOptions { IncludeUnicodeCoverage = true };
            Assert.That(FontMetadataReader.Read(font).Faces.Single().UnicodeCoverage, Is.Null);
            Assert.That(FontMetadataReader.Read(withoutCmap, options).Faces.Single().UnicodeCoverage!.Status, Is.EqualTo(FontSectionStatus.Missing));
            var coverage = FontMetadataReader.Read(font, options).Faces.Single().UnicodeCoverage!;
            Assert.That(coverage.Status, Is.EqualTo(FontSectionStatus.Complete));
            var list = (IList<FontUnicodeRange>)coverage.Ranges;
            Assert.That(() => list.Clear(), Throws.TypeOf<NotSupportedException>());
            font.AsSpan().Clear();
            Assert.That(Intervals(coverage), Is.EqualTo(new[] { (65, 65) }));
        }
        [TestCase(false, nameof(FontReadLimits.MaxTotalCmapWork))]
        [TestCase(true, nameof(FontReadLimits.MaxTotalUnicodeRanges))]
        public void ReaderRejectsNonPositiveCmapBudgets(bool rangeLimit, string parameter) {
            var limits = rangeLimit
                ? new FontReadLimits { MaxTotalUnicodeRanges = -1 }
                : new FontReadLimits { MaxTotalCmapWork = 0 };
            var options = new FontReadOptions { IncludeUnicodeCoverage = true, Limits = limits };
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => FontMetadataReader.Inspect(Sfnt(), options));
            Assert.That(exception!.ParamName, Is.EqualTo(parameter));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void DistinctCmapsShareBudgetsAndAReusedTtcTableIsChargedOnce(bool shareTable) {
            byte[] firstTable = Cmap((3, 10, Sequential((65, 65, 1))));
            byte[] secondTable = Cmap((3, 10, Sequential((66, 66, 1))));
            byte[] font = Collection([("cmap", firstTable)], [("cmap", secondTable)]);
            if(shareTable) {
                int firstFace = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(CollectionFacePosition(0)));
                int secondFace = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(CollectionFacePosition(1)));
                uint sharedOffset = BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(firstFace + TableOffsetPosition(0)));
                U32(font, secondFace + TableOffsetPosition(0), sharedOffset);
            }
            var options = new FontReadOptions {
                IncludeUnicodeCoverage = true,
                Limits = new FontReadLimits { MaxTotalCmapWork = 2, MaxTotalUnicodeRanges = 1 }
            };
            var faces = FontMetadataReader.Inspect(font, options).Metadata!.Faces;
            Assert.That(faces[0].UnicodeCoverage!.Status, Is.EqualTo(FontSectionStatus.Complete));
            var second = faces[1].UnicodeCoverage!;
            Assert.That(second.Status, Is.EqualTo(shareTable ? FontSectionStatus.Complete : FontSectionStatus.NotRead));
            if(shareTable)
                Assert.That(Intervals(second), Is.EqualTo(new[] { (65, 65) }));
            else
                Assert.That(second.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.LimitExceeded));
        }
        [Test]
        public void ContainerRangeBudgetAppliesEvenWhenWorkRemains() {
            byte[] table = Cmap((3, 10, Sequential((65, 65, 1))));
            byte[] font = Collection([("cmap", table)], [("cmap", table)]);
            var options = new FontReadOptions { IncludeUnicodeCoverage = true, Limits = new FontReadLimits { MaxTotalUnicodeRanges = 1 } };
            var faces = FontMetadataReader.Inspect(font, options).Metadata!.Faces;
            Assert.That(faces[0].UnicodeCoverage!.Status, Is.EqualTo(FontSectionStatus.Complete));
            var second = faces[1].UnicodeCoverage!;
            Assert.That(second.Status, Is.EqualTo(FontSectionStatus.NotRead));
            Assert.That(second.Format, Is.EqualTo(12));
            Assert.That(second.Diagnostics.Single().TableByteOffset, Is.EqualTo(28));
        }
        [TestCase(1)]
        [TestCase(2)]
        public void DifferentCatalogTablesShareOneDiagnosticBudget(int limit) {
            byte[] font = Collection([("cmap", new byte[1])], [("fvar", new byte[1])]);
            var options = new FontReadOptions {
                IncludeUnicodeCoverage = true,
                IncludeVariationAxes = true,
                Limits = new FontReadLimits { MaxDiagnostics = limit }
            };
            var result = FontMetadataReader.Inspect(font, options);
            var coverage = result.Faces[0].UnicodeCoverage!;
            var variations = result.Faces[1].VariationAxes!;
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete));
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(coverage.Status, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(variations.Status, Is.EqualTo(FontSectionStatus.Invalid));
            var codes = coverage.Diagnostics.Concat(variations.Diagnostics).Select(d => d.Code);
            string[] expected = limit == 1 ? [FontDiagnosticCodes.DiagnosticsTruncated] :
                [FontDiagnosticCodes.InvalidCmapTable, FontDiagnosticCodes.InvalidFvarTable];
            Assert.That(codes, Is.EqualTo(expected));
        }
        [TestCase(1)]
        [TestCase(2)]
        public void LaterBaselineDiagnosticKeepsPriorityAndBothSectionsRetainStatus(int limit) {
            byte[] font = Collection([("cmap", new byte[1])], [("name", new byte[1])]);
            var options = new FontReadOptions { IncludeUnicodeCoverage = true, Limits = new FontReadLimits { MaxDiagnostics = limit } };
            var result = FontMetadataReader.Inspect(font, options);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.InvalidNameTable));
            var coverage = result.Faces[0].UnicodeCoverage!;
            Assert.That(coverage.Status, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(coverage.Diagnostics.Count, Is.EqualTo(limit - 1));
            Assert.That(result.Faces[1].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
        }
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        public void SharedPhysicalTableReportsItsDiagnosticOnce(int limit, bool overTableLimit) {
            byte[] table = Cmap((3, 0, new byte[] { 0, 4 }));
            byte[] font = WebFontFixtures.Woff2([new WebFontFixtures.Table("cmap", table)], [[0], [0]]);
            var options = new FontReadOptions {
                IncludeUnicodeCoverage = true,
                Limits = new FontReadLimits { MaxDiagnostics = limit, MaxTableBytes = overTableLimit ? 1 : table.Length }
            };
            var result = FontMetadataReader.Inspect(font, options);
            var first = result.Faces[0].UnicodeCoverage!;
            Assert.That(result.Faces[1].UnicodeCoverage, Is.SameAs(first));
            Assert.That(first.Diagnostics.Single().Code, Is.EqualTo(overTableLimit ?
                FontDiagnosticCodes.LimitExceeded : FontDiagnosticCodes.UnsupportedCmapSubtable));
        }
        static FontUnicodeCoverage Parse(byte[] table, FontReadLimits? limits = null) {
            return CmapParser.Read(table, 100, new ReadingContext(limits ?? new FontReadLimits(), CancellationToken.None));
        }
        static (int Start, int End)[] Intervals(FontUnicodeCoverage coverage) {
            return coverage.Ranges.Select(range => (range.Start, range.End)).ToArray();
        }
        static byte[] Cmap(params (int Platform, int Encoding, byte[] Data)[] records) {
            int cursor = EncodingRecords + EncodingRecordSize * records.Length;
            byte[] table = new byte[cursor + records.Sum(record => record.Data.Length)];
            U16(table, 2, records.Length);
            for(int i = 0; i < records.Length; i++) {
                var (platform, encoding, data) = records[i];
                int position = EncodingRecords + i * EncodingRecordSize;
                U16(table, position, platform);
                U16(table, position + 2, encoding);
                U32(table, position + EncodingSubtableOffset, (uint)cursor);
                data.CopyTo(table, cursor);
                cursor += data.Length;
            }
            return table;
        }
        static byte[] Sequential(params (uint Start, uint End, uint FirstGlyph)[] groups) {
            byte[] subtable = new byte[Format12Groups + Format12GroupSize * groups.Length];
            U16(subtable, 0, 12);
            U32(subtable, 4, (uint)subtable.Length);
            U32(subtable, 12, (uint)groups.Length);
            for(int i = 0; i < groups.Length; i++) {
                var (start, end, glyph) = groups[i];
                int position = Format12Groups + i * Format12GroupSize;
                U32(subtable, position, start);
                U32(subtable, position + 4, end);
                U32(subtable, position + 8, glyph);
            }
            return subtable;
        }
    }
}
