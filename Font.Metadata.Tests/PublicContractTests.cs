using System;
using NUnit.Framework;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class PublicContractTests {
        [TestCase(typeof(FontLicenseAnalyzer), "Fonts.Metadata.FontLicenseAnalyzer")]
        [TestCase(typeof(FontLicenseAnalysis), "Fonts.Metadata.FontLicenseAnalysis")]
        [TestCase(typeof(FontLicenseHint), "Fonts.Metadata.FontLicenseHint")]
        [TestCase(typeof(FontLicenseIdentifierKind), "Fonts.Metadata.FontLicenseIdentifierKind")]
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
