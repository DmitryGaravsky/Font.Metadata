using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Fonts.Metadata.Internal;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class FvarReadingTests {
        // Independent big-endian wire vectors. The first axis spans the signed Fixed domain and the second has a degenerate range.
        const string SlantRecord = "736C6E74" + "80000000" + "FFFFFFFF" + "7FFFFFFF" + "0001" + "0100";
        const string OpticalRecord = "6F70737A" + "00088000" + "00088000" + "00088000" + "0000" + "0101";
        const string OneAxisHeader = "00010000" + "0010" + "0002" + "0001" + "0014" + "0000" + "0008";
        const string TwoAxesHeader = "00010000" + "0010" + "0002" + "0002" + "0014" + "0000" + "000C";
        const int HeaderSize = 16, AxisSize = 20, AxisTagSize = 4;
        const int AxesArrayOffsetField = 4, ReservedField = 6, AxisCountField = 8, AxisSizeField = 10;
        const int InstanceCountField = 12, InstanceSizeField = 14;
        [Test]
        public void ReadsExactSignedCoordinatesAtDeclaredOffsetAndLinksThroughSourceNameIds() {
            const string shiftedHeader = "00010000" + "0014" + "0002" + "0002" + "0014" + "0000" + "000C" + "DEADBEEF";
            var names = Names(Name.Text(256, "Slant"), Name.Text(256, "Inclinaison", language: 0x040c));
            var font = Sfnt(("fvar", Convert.FromHexString(shiftedHeader + SlantRecord + OpticalRecord)), ("name", names));
            var face = FontMetadataReader.Read(font, Options()).Faces.Single();
            var result = face.VariationAxes!;
            Array.Clear(font);
            Assert.That(result.Status, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Axes.Select(axis => (axis.Index, axis.Tag, axis.Minimum, axis.Default, axis.Maximum, axis.Flags, axis.NameId)),
                Is.EqualTo(new[] {
                    (0, "slnt", -32768m, -0.0000152587890625m, 32767.9999847412109375m, (ushort)1, (ushort)256),
                    (1, "opsz", 8.5m, 8.5m, 8.5m, (ushort)0, (ushort)257)
                }));
            var slantNames = face.NameRecords.Where(name => name.NameId == result.Axes[0].NameId);
            Assert.That(slantNames.Select(name => name.Value), Is.EqualTo(new[] { "Slant", "Inclinaison" }));
            Assert.That(face.NameRecords.Any(name => name.NameId == result.Axes[1].NameId), Is.False);
            Assert.Throws<NotSupportedException>(() => ((IList<FontVariationAxis>)result.Axes).Clear());
        }
        [Test]
        public void ReaderDistinguishesSkippedAxesFromAnAbsentTable() {
            var malformed = Sfnt(("fvar", new byte[] { 0 }));
            Assert.That(FontMetadataReader.Read(malformed).Faces.Single().VariationAxes, Is.Null);
            var missing = FontMetadataReader.Read(Example(), Options()).Faces.Single().VariationAxes!;
            Assert.That(missing.Status, Is.EqualTo(FontSectionStatus.Missing));
            Assert.That(missing.Axes, Is.Empty);
            Assert.That(missing.HasTableEntry, Is.False);
            var invalid = FontMetadataReader.Read(malformed, Options()).Faces.Single().VariationAxes!;
            Assert.That(invalid.HasTableEntry, Is.True);
            Assert.That(invalid.Status, Is.EqualTo(FontSectionStatus.Invalid));
        }
        [TestCase(0, 0)]
        [TestCase(3, 1)]
        [TestCase(ushort.MaxValue, ushort.MaxValue)]
        public void CompleteAxesDoNotRequireValidInstanceDeclarationsOrBytes(int instanceCount, int instanceSize) {
            var table = Convert.FromHexString(OneAxisHeader + SlantRecord + "FF");
            U16(table, InstanceCountField, instanceCount);
            U16(table, InstanceSizeField, instanceSize);
            var axes = FontMetadataReader.Read(Sfnt(("fvar", table)), Options()).Faces.Single().VariationAxes!;
            Assert.That(axes.Status, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(axes.Axes.Single().Tag, Is.EqualTo("slnt"));
            Assert.That(axes.Diagnostics, Is.Empty);
        }
        [Test]
        public void ZeroAxesYieldAnEmptyCompleteSection() {
            var table = Convert.FromHexString("00010000001000020000001400000004");
            var axes = FontMetadataReader.Read(Sfnt(("fvar", table)), Options()).Faces.Single().VariationAxes!;
            Assert.That(axes.Status, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(axes.Axes, Is.Empty);
        }
        [TestCase(3, FontSectionStatus.Invalid, 0)]
        [TestCase(15, FontSectionStatus.Invalid, 0)]
        [TestCase(16, FontSectionStatus.Invalid, 0)]
        [TestCase(35, FontSectionStatus.Invalid, 0)]
        [TestCase(36, FontSectionStatus.Partial, 1)]
        [TestCase(55, FontSectionStatus.Partial, 1)]
        [TestCase(56, FontSectionStatus.Complete, 2)]
        public void TruncatedAxisArrayKeepsOnlyCompleteRecords(int length, FontSectionStatus status, int count) {
            var table = Convert.FromHexString(TwoAxesHeader + SlantRecord + OpticalRecord);
            var result = FontMetadataReader.Inspect(Sfnt(("fvar", table[..length])), Options());
            var axes = result.Metadata!.Faces.Single().VariationAxes!;
            Assert.That(axes.Status, Is.EqualTo(status));
            Assert.That(axes.Axes.Count, Is.EqualTo(count));
            if(status != FontSectionStatus.Complete)
                Assert.That(axes.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.InvalidFvarTable));
        }
        [TestCase(AxesArrayOffsetField, 15, FontSectionStatus.Invalid, FontDiagnosticCodes.InvalidFvarTable)]
        [TestCase(AxesArrayOffsetField, 37, FontSectionStatus.Invalid, FontDiagnosticCodes.InvalidFvarTable)]
        [TestCase(AxisSizeField, 19, FontSectionStatus.Invalid, FontDiagnosticCodes.InvalidFvarTable)]
        [TestCase(AxisSizeField, 21, FontSectionStatus.Invalid, FontDiagnosticCodes.InvalidFvarTable)]
        [TestCase(0, 2, FontSectionStatus.Unsupported, FontDiagnosticCodes.UnsupportedTableVersion)]
        [TestCase(2, 1, FontSectionStatus.Unsupported, FontDiagnosticCodes.UnsupportedTableVersion)]
        public void RejectsUnusableAxisHeaders(int field, int value, FontSectionStatus status, string code) {
            var table = Convert.FromHexString(OneAxisHeader + SlantRecord);
            U16(table, field, value);
            var axes = FontMetadataReader.Inspect(Sfnt(("fvar", table)), Options()).Metadata!.Faces.Single().VariationAxes!;
            Assert.That(axes.Status, Is.EqualTo(status));
            Assert.That(axes.Axes, Is.Empty);
            Assert.That(axes.Diagnostics.Single().Code, Is.EqualTo(code));
        }
        [Test]
        public void MaximumDeclaredAxisCountKeepsTheAvailablePrefix() {
            var table = Convert.FromHexString(OneAxisHeader + SlantRecord);
            U16(table, AxisCountField, ushort.MaxValue);
            var axes = FontMetadataReader.Inspect(Sfnt(("fvar", table)), Options()).Metadata!.Faces.Single().VariationAxes!;
            Assert.That(axes.Status, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(axes.Axes.Single().Index, Is.Zero);
            Assert.That(axes.Diagnostics.Single().TableByteOffset, Is.EqualTo(HeaderSize + AxisSize));
        }
        [Test]
        public void MalformedAndDuplicateAxesLeaveLaterVerifiedIndexesIntact() {
            const string reversedSlant = "736C6E74" + "00010000" + "FFFF0000" + "7FFF0000" + "0001" + "0100";
            const string badTag = "6F70737F" + "00000000" + "00000000" + "00000000" + "0000" + "0102";
            var table = Convert.FromHexString(OneAxisHeader + reversedSlant + SlantRecord + SlantRecord + badTag + OpticalRecord);
            U16(table, AxisCountField, 5);
            var font = Sfnt(("fvar", table));
            var result = FontMetadataReader.Inspect(font, Options());
            var axes = result.Metadata!.Faces.Single().VariationAxes!;
            Assert.That(axes.Status, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(axes.Axes.Select(axis => (axis.Index, axis.Tag)), Is.EqualTo(new[] { (1, "slnt"), (4, "opsz") }));
            var tableOffsets = new int[] { 24, 56, 76 };
            Assert.That(axes.Diagnostics.Select(item => item.TableByteOffset), Is.EqualTo(tableOffsets));
            var origin = ReadOffset(font, TableOffsetPosition(0));
            Assert.That(axes.Diagnostics.Select(item => item.ByteOffset), Is.EqualTo(tableOffsets.Select(offset => origin + offset)));
            Assert.That(axes.Diagnostics.All(item => item.Code == FontDiagnosticCodes.InvalidFvarTable && item.TableTag == "fvar"), Is.True);
            Assert.Throws<NotSupportedException>(() => ((IList<FontDiagnostic>)axes.Diagnostics).Clear());
        }
        [Test]
        public void KeepsRawNonconformingFlagsAndNameId() {
            const string custom = "58314F50" + "00000000" + "00000000" + "00010000" + "8001" + "FFFF";
            var table = Convert.FromHexString(OneAxisHeader + custom);
            U16(table, ReservedField, 0);
            var axes = FontMetadataReader.Inspect(Sfnt(("fvar", table)), Options()).Metadata!.Faces.Single().VariationAxes!;
            Assert.That(axes.Status, Is.EqualTo(FontSectionStatus.Complete));
            var axis = axes.Axes.Single();
            Assert.That((axis.Tag, axis.Flags, axis.NameId), Is.EqualTo(("X1OP", (ushort)0x8001, ushort.MaxValue)));
            Assert.That(axes.Diagnostics.Select(item => item.TableByteOffset), Is.EqualTo(new[] { 6, 32, 34 }));
            Assert.That(axes.Diagnostics.All(item => item.Code == FontDiagnosticCodes.NonConformantFvarTable &&
                item.Severity == FontDiagnosticSeverity.Warning), Is.True);
        }
        [Test]
        public void AxisBudgetSpansCollectionFacesAndSharedTablesConsumeItOnce() {
            var single = Convert.FromHexString(OneAxisHeader + SlantRecord);
            var pair = Convert.FromHexString(TwoAxesHeader + SlantRecord + OpticalRecord);
            var font = Collection([("fvar", single)], [("fvar", single)], [("fvar", pair)], [("fvar", single)]);
            int first = ReadOffset(font, CollectionFacePosition(0)), second = ReadOffset(font, CollectionFacePosition(1));
            U32(font, second + TableOffsetPosition(0), (uint)ReadOffset(font, first + TableOffsetPosition(0)));
            var result = FontMetadataReader.Inspect(font, Options(new FontReadLimits { MaxTotalVariationAxes = 2 }));
            var sections = result.Metadata!.Faces.Select(face => face.VariationAxes!).ToArray();
            Assert.That(sections.Select(item => item.Status), Is.EqualTo(new[] {
                FontSectionStatus.Complete, FontSectionStatus.Complete, FontSectionStatus.Partial, FontSectionStatus.NotRead
            }));
            Assert.That(sections.Select(item => item.Axes.Count), Is.EqualTo(new[] { 1, 1, 1, 0 }));
            Assert.That(sections[2].Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.LimitExceeded));
            Assert.That(sections[2].Diagnostics.Single().LimitReason, Is.EqualTo("MaxTotalVariationAxes"));
            Assert.That(sections[3].Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.LimitExceeded));
        }
        [Test]
        public void InvalidRecordsConsumeTheSameBudgetAsVerifiedAxes() {
            var bad = Convert.FromHexString(OneAxisHeader + SlantRecord);
            bad[HeaderSize] = (byte)'0';
            var good = Convert.FromHexString(OneAxisHeader + OpticalRecord);
            var font = Collection([("fvar", bad)], [("fvar", good)]);
            var result = FontMetadataReader.Inspect(font, Options(new FontReadLimits { MaxTotalVariationAxes = 1 }));
            var sections = result.Metadata!.Faces.Select(face => face.VariationAxes!).ToArray();
            Assert.That(sections[0].Status, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(sections[0].Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.InvalidFvarTable));
            Assert.That(sections[1].Status, Is.EqualTo(FontSectionStatus.NotRead));
            Assert.That(sections[1].Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.LimitExceeded));
        }
        [TestCase(0)]
        [TestCase(-1)]
        public void AxisBudgetMustBePositive(int limit) {
            var options = Options(new FontReadLimits { MaxTotalVariationAxes = limit });
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => FontMetadataReader.Inspect(Example(), options));
            Assert.That(error!.ParamName, Is.EqualTo(nameof(FontReadLimits.MaxTotalVariationAxes)));
        }
        [Test]
        public void TableLimitIsReportedInTheUnavailableAxesSection() {
            var table = Convert.FromHexString(OneAxisHeader + SlantRecord);
            var options = Options(new FontReadLimits { MaxTableBytes = HeaderSize });
            var result = FontMetadataReader.Inspect(Sfnt(("fvar", table)), options);
            var axes = result.Metadata!.Faces.Single().VariationAxes!;
            Assert.That(axes.Status, Is.EqualTo(FontSectionStatus.NotRead));
            Assert.That(axes.Axes, Is.Empty);
            var diagnostic = axes.Diagnostics.Single();
            Assert.That(diagnostic.Code, Is.EqualTo(FontDiagnosticCodes.LimitExceeded));
            Assert.That(diagnostic.TableTag, Is.EqualTo("fvar"));
        }
        [Test]
        public void CancellationDuringAxisTraversalPropagatesTheOperationToken() {
            var table = Convert.FromHexString(TwoAxesHeader + SlantRecord + OpticalRecord);
            using(var cancellation = new CancellationTokenSource()) {
                int checks = 0;
                void CheckCancellation() {
                    if(++checks == 3)
                        cancellation.Cancel();
                    cancellation.Token.ThrowIfCancellationRequested();
                }
                var context = new ReadingContext(new FontReadLimits(), cancellation.Token, CheckCancellation);
                var error = Assert.Throws<OperationCanceledException>(() => FvarParser.Read(table, 0, context));
                Assert.That(error!.CancellationToken, Is.EqualTo(cancellation.Token));
            }
        }
        [Test]
        public void Woff2ReportsLogicalAxisOffsetsWithACompressedAnchor() {
            var table = Convert.FromHexString(TwoAxesHeader + SlantRecord + OpticalRecord);
            const int secondAxisOffset = HeaderSize + AxisSize;
            table[secondAxisOffset + AxisTagSize - 1] = 0x80;
            var font = WebFontFixtures.Woff2([new WebFontFixtures.Table("fvar", table)]);
            var result = FontMetadataReader.Inspect(font, Options());
            var axes = result.Metadata!.Faces.Single().VariationAxes!;
            Assert.That(axes.Status, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(axes.Axes.Single().Tag, Is.EqualTo("slnt"));
            var diagnostic = axes.Diagnostics.Single();
            Assert.That(diagnostic.TableByteOffset, Is.EqualTo(secondAxisOffset));
            Assert.That(diagnostic.ByteOffset, Is.EqualTo(54));
        }
        [Test]
        public void Woff2IncompleteSourcePreservesAxesAndTableDiagnostics() {
            const int lengthField = 8, compressedLengthField = 20;
            var table = Convert.FromHexString(OneAxisHeader + SlantRecord);
            U16(table, ReservedField, 0);
            var encoded = WebFontFixtures.Woff2([new WebFontFixtures.Table("fvar", table)]);
            byte[] font = [.. encoded, 123];
            U32(font, lengthField, (uint)font.Length);
            U32(font, compressedLengthField, (uint)(ReadOffset(font, compressedLengthField) + 1));
            var result = FontMetadataReader.Inspect(font, Options());
            var axes = result.Metadata!.Faces.Single().VariationAxes!;
            Assert.That(axes.Status, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(axes.Axes.Single().Tag, Is.EqualTo("slnt"));
            Assert.That(axes.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.NonConformantFvarTable));
            Assert.That(result.Diagnostics.Any(item => item.Code == FontDiagnosticCodes.InvalidCompressedData), Is.True);
        }
        static FontReadOptions Options(FontReadLimits? limits = null) {
            return new FontReadOptions { IncludeVariationAxes = true, Limits = limits ?? new FontReadLimits() };
        }
    }
}
