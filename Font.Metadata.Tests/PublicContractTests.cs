using System;
using NUnit.Framework;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class PublicContractTests {
        [Test]
        public void AdditiveEotResultsAreImmutableAndInputLimitsDoNotInventFacts() {
            byte[] input = WebFontFixtures.Eot(FontFixtures.Sfnt(("name", WebFontFixtures.NameData())));
            var result = FontMetadataReader.Read(input);
            foreach(Type type in new[] { typeof(FontMetadata), typeof(FontMetadata.Face), typeof(FontMetadata.EotHeader), typeof(FontDiagnostic) }) {
                Assert.That(type.GetConstructors(), Is.Empty);
                foreach(var property in type.GetProperties())
                    Assert.That(property.GetSetMethod(), Is.Null, property.Name);
            }
            Assert.That(result.Eot!.FamilyName, Is.EqualTo("Container Family"));
            Array.Fill(input, (byte)0);
            Assert.That(result.Eot.FamilyName, Is.EqualTo("Container Family"));
            input = WebFontFixtures.Eot(FontFixtures.Sfnt(("name", WebFontFixtures.NameData())));
            var limited = FontMetadataReader.Inspect(input, new FontReadOptions { Limits = new FontReadLimits { MaxInputBytes = 36 } });
            Assert.That(limited.Metadata!.EotHeaderStatus, Is.EqualTo(FontSectionStatus.NotRead));
            Assert.That(limited.Metadata.Eot, Is.Null);
        }
        [Test]
        public void EotUtf16SurrogatePairCanCrossValidationChunkBoundary() {
            string family = new string('A', 4095) + char.ConvertFromUtf32(0x1f600);
            byte[] input = WebFontFixtures.Eot(FontFixtures.Sfnt(("name", WebFontFixtures.NameData())), family: family);
            var result = FontMetadataReader.Inspect(input);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete));
            Assert.That(result.Metadata!.Eot!.FamilyName, Is.EqualTo(family));
        }
        [TestCase(typeof(FontLicenseAnalyzer), "Fonts.Metadata.FontLicenseAnalyzer")]
        [TestCase(typeof(FontLicenseAnalysis), "Fonts.Metadata.FontLicenseAnalysis")]
        [TestCase(typeof(FontLicenseHint), "Fonts.Metadata.FontLicenseHint")]
        [TestCase(typeof(FontLicenseIdentifierKind), "Fonts.Metadata.FontLicenseIdentifierKind")]
        [TestCase(typeof(FontFaceSource), "Fonts.Metadata.FontFaceSource")]
        [TestCase(typeof(FontMetadata), "Fonts.Metadata.FontMetadata")]
        [TestCase(typeof(FontMetadata.Face), "Fonts.Metadata.FontMetadata+Face")]
        [TestCase(typeof(FontMetadata.NameRecord), "Fonts.Metadata.FontMetadata+NameRecord")]
        [TestCase(typeof(FontMetadata.EotHeader), "Fonts.Metadata.FontMetadata+EotHeader")]
        public void ContractTypesAreAccessibleFromTheConsumerFramework(Type type, string fullName) {
            using(Assert.EnterMultipleScope()) {
                Assert.That(type.FullName, Is.EqualTo(fullName));
                Assert.That(type.Assembly.GetName().Name, Is.EqualTo("Font.Metadata"));
                Assert.That(type.IsVisible, Is.True);
            }
        }
    }
}
