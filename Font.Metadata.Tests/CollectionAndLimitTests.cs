using System;
using System.Linq;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class CollectionAndLimitTests {
        static FontReadOptions Options(FontReadLimits limits) {
            return new FontReadOptions { Limits = limits };
        }
        [TestCase(false)]
        [TestCase(true)]
        public void CollectionsUseAbsoluteTableOffsetsAndOriginalFaceIndexes(bool version2) {
            var good = new[] { ("name", Names(Name.Text(FamilyNameId, "A"))), ("OS/2", Os2()) };
            var bad = new[] { ("name", new byte[2]), ("OS/2", Os2(8)) };
            byte[] font = version2 ? CollectionV2(good, bad, good) : Collection(good, bad, good);
            var result = FontMetadataReader.Inspect(font);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Format, Is.EqualTo(FontFormat.Collection));
            Assert.That(result.Metadata.Faces.Select(x => x.Index), Is.EqualTo([0, 1, 2]));
            Assert.That(result.Metadata.Faces.Select(x => x.FamilyName), Is.EqualTo(["A", null, "A"]));
            Assert.That(result.Metadata.Faces[1].EmbeddingPermissions, Is.EqualTo(8));
            Assert.That(result.Diagnostics.Single().FaceIndex, Is.EqualTo(1));
        }
        [Test]
        public void BadMiddleDirectoryLeavesPlaceholderAndDoesNotRenumberFaces() {
            var good = new[] { ("name", Names(Name.Text(FamilyNameId, "A"))) };
            byte[] font = Collection(good, good, good);
            U32(font, CollectionFacePosition(1), uint.MaxValue);
            var result = FontMetadataReader.Inspect(font);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[1].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            Assert.That(result.Metadata.Faces[2].Index, Is.EqualTo(2));
            Assert.That(result.Metadata.Faces[2].FamilyName, Is.EqualTo("A"));
        }
        [Test]
        public void SharedTablesConsumeStringBudgetOnceAndLocalizeDiagnosticsPerFace() {
            byte[] names = Names(Name.Text(FamilyNameId, "A"), new Name(LicenseDescriptionNameId, [0xd8, 0]));
            byte[] font = Collection([("name", names)], [("name", names)]);
            int first = ReadOffset(font, CollectionFacePosition(0)), second = ReadOffset(font, CollectionFacePosition(1));
            U32(font, second + TableOffsetPosition(0), (uint)ReadOffset(font, first + TableOffsetPosition(0)));
            var result = FontMetadataReader.Inspect(font, Options(new FontReadLimits { MaxTotalNameStringBytes = 4 }));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces.Select(x => x.FamilyName), Is.EqualTo(["A", "A"]));
            Assert.That(result.Diagnostics.Select(x => x.FaceIndex), Is.EqualTo([0, 1]));
            Assert.That(result.Diagnostics.All(x => x.Code == FontDiagnosticCodes.InvalidNameTable), Is.True);
        }
        [Test]
        public void TablesCannotPointIntoAnotherFacesDirectory() {
            var good = new[] { ("name", Names(Name.Text(FamilyNameId, "A"))) };
            byte[] font = Collection(good, good);
            int first = ReadOffset(font, CollectionFacePosition(0)), second = ReadOffset(font, CollectionFacePosition(1));
            U32(font, first + TableOffsetPosition(0), (uint)second);
            var result = FontMetadataReader.Inspect(font);
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(result.Metadata.Faces[1].FamilyName, Is.EqualTo("A"));
        }
        [Test]
        public void FaceLimitRetainsBoundedResultsAndSignalsIncompleteCollection() {
            var good = new[] { ("name", Names(Name.Text(FamilyNameId, "A"))) };
            var result = FontMetadataReader.Inspect(Collection(good, good), Options(new FontReadLimits { MaxFaces = 1 }));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces.Count, Is.EqualTo(1));
            Assert.That(result.Diagnostics.Single().Category, Is.EqualTo(FontDiagnosticCategory.LimitExceeded));
        }
        [Test]
        public void InvalidCollectionCountDoesNotAllocatePlaceholders() {
            byte[] font = Collection([("name", Names(Name.Text(FamilyNameId, "A")))]);
            U32(font, CollectionFaceCountOffset, uint.MaxValue);
            var result = FontMetadataReader.Inspect(font);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(result.Metadata!.Faces, Is.Empty);
        }
        [Test]
        public void InputAndDirectoryLimitsAreExplicitFailures() {
            var result = FontMetadataReader.Inspect(Example(), Options(new FontReadLimits { MaxInputBytes = 4 }));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(result.Metadata!.Faces, Is.Empty);
            result = FontMetadataReader.Inspect(Example(), Options(new FontReadLimits { MaxTablesPerFace = 1 }));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            Assert.That(result.Diagnostics[0].Category, Is.EqualTo(FontDiagnosticCategory.LimitExceeded));
        }
        [Test]
        public void TableLimitPreservesIndependentOs2() {
            var result = FontMetadataReader.Inspect(Sfnt(("name", Names(Name.Text(FamilyNameId, new string('A', 80)))), ("OS/2", Os2(length: 62))),
                Options(new FontReadLimits { MaxTableBytes = 70 }));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            Assert.That(result.Metadata.Faces[0].VendorId, Is.EqualTo("TEST"));
        }
        [Test]
        public void RecordAndStringLimitsPreserveEarlierRecords() {
            byte[] font = Sfnt(("name", Names(Name.Text(FamilyNameId, "A"), Name.Text(LicenseDescriptionNameId, "B"))));
            foreach(var limits in new[] { new FontReadLimits { MaxNameRecords = 1 }, new FontReadLimits { MaxNameStringBytes = 2 }, new FontReadLimits { MaxTotalNameStringBytes = 2 } }) {
                var result = FontMetadataReader.Inspect(font, Options(limits));
                Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
                Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Partial));
                Assert.That(result.Metadata.Faces[0].FamilyName, Is.EqualTo("A"));
                Assert.That(result.Diagnostics.Any(x => x.Category == FontDiagnosticCategory.LimitExceeded), Is.True);
            }
        }
        [Test]
        public void RepeatedStringReferencesAreDecodedOnceButRecordsAreNotDeduplicated() {
            byte[] names = Names(Name.Text(FamilyNameId, "A"), Name.Text(LicenseDescriptionNameId, "B"));
            U16(names, NameRecordPosition(1) + NameStringOffsetField, 0);
            var result = FontMetadataReader.Inspect(Sfnt(("name", names)), Options(new FontReadLimits { MaxNameStringBytes = 2 }));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete));
            Assert.That(result.Metadata!.Faces[0].NameRecords.Select(x => x.Value), Is.EqualTo(["A", "A"]));
        }
        [Test]
        public void FailedDecodingDoesNotResetCollectionBudget() {
            byte[] font = Collection([("name", Names(new Name(FamilyNameId, [0xd8, 0])))], [("name", Names(Name.Text(FamilyNameId, "A")))]);
            var result = FontMetadataReader.Inspect(font, Options(new FontReadLimits { MaxTotalNameStringBytes = 2 }));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(result.Metadata.Faces[1].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
        }
        [Test]
        public void DiagnosticSuppressionNeverTurnsPartialReadingIntoComplete() {
            byte[] font = Sfnt(("name", Names(Name.Text(FamilyNameId, "A"), new Name(SubfamilyNameId, [0xd8, 0]), new Name(3, [0xd8, 0]))));
            var result = FontMetadataReader.Inspect(font, Options(new FontReadLimits { MaxDiagnostics = 1 }));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo(FontDiagnosticCodes.DiagnosticsTruncated));
            Assert.That(result.Metadata!.Faces[0].FamilyName, Is.EqualTo("A"));
        }
    }
}
