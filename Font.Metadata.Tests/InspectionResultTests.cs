using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class InspectionResultTests {
        [TestCase(0, FontReadStatus.NotRecognized)]
        [TestCase(8, FontReadStatus.Failed)]
        public void AbsentMetadataHasReadOnlyEmptyFaces(int inputSize, FontReadStatus expectedStatus) {
            var result = FontMetadataReader.Inspect(new byte[inputSize],
                new FontReadOptions { Limits = new FontReadLimits { MaxInputBytes = 4 } });
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.Metadata, Is.Null);
            Assert.That(result.Faces, Is.Empty);
            Assert.Throws<NotSupportedException>(() => ((IList<FontMetadata.Face>)result.Faces).Clear());
        }
        [Test]
        public void CompleteInspectionExposesOriginalReadOnlyFacesAndRecords() {
            byte[] input = Sfnt(("name", Names(Name.Text(FamilyNameId, "A"), Name.Text(LicenseDescriptionNameId, "MIT"))));
            var result = FontMetadataReader.Inspect(input);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete));
            Assert.That(result.Faces, Has.Count.EqualTo(1));
            Assert.That(result.Faces[0], Is.SameAs(result.Metadata!.Faces[0]));
            Array.Clear(input);
            Assert.That(result.Faces[0].FamilyName, Is.EqualTo("A"));
            var analysis = FontLicenseAnalyzer.Analyze(result.Faces[0]);
            Assert.That(analysis.Hints[0].SourceRecord, Is.SameAs(result.Faces[0].NameRecords[1]));
            Assert.Throws<NotSupportedException>(() => ((IList<FontMetadata.Face>)result.Faces).Clear());
        }
        [Test]
        public void PartialInspectionPreservesPlaceholdersAndOriginalIndexes() {
            var face = new[] { ("name", Names(Name.Text(FamilyNameId, "A"))) };
            byte[] input = Collection(face, face, face);
            U32(input, CollectionFacePosition(1), uint.MaxValue);
            var result = FontMetadataReader.Inspect(input);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Faces.Select(item => item.Index), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(result.Faces.Select(item => item.FamilyName), Is.EqualTo(new string?[] { "A", null, "A" }));
            Assert.That(result.Faces[1].NameTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            for(int index = 0; index < result.Faces.Count; index++)
                Assert.That(result.Faces[index], Is.SameAs(result.Metadata!.Faces[index]));
        }
        [Test]
        public void FailedInspectionCanRetainAnUnreadableFace() {
            byte[] input = Sfnt(("name", new byte[1]));
            var result = FontMetadataReader.Inspect(input);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(result.Faces, Has.Count.EqualTo(1));
            Assert.That(result.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
            var exception = Assert.Throws<FontReadException>(() => FontMetadataReader.Read(input))!;
            Assert.That(exception.Result.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
        }
        [Test]
        public void EmptyFacesDoNotHideRecognizedContainerMetadata() {
            byte[] input = Sfnt(("name", Names(Name.Text(FamilyNameId, "A"))));
            var result = FontMetadataReader.Inspect(input,
                new FontReadOptions { Limits = new FontReadLimits { MaxInputBytes = 4 } });
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(result.Metadata!.Format, Is.EqualTo(FontFormat.Sfnt));
            Assert.That(result.Faces, Is.Empty);
            Assert.Throws<NotSupportedException>(() => ((IList<FontMetadata.Face>)result.Faces).Clear());
        }
        [Test]
        public void UnsupportedCollectionHasEmptyFacesAndRetainsItsFormat() {
            byte[] input = Collection(new[] { ("name", Names(Name.Text(FamilyNameId, "A"))) });
            U32(input, CollectionVersionOffset, 0x00030000);
            var result = FontMetadataReader.Inspect(input);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Unsupported));
            Assert.That(result.Metadata!.Format, Is.EqualTo(FontFormat.Collection));
            Assert.That(result.Faces, Is.Empty);
        }
    }
}
