using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class SfntReadingTests {
        [TestCase(0x00010000u)]
        [TestCase(0x4f54544fu)]
        [TestCase(0x74727565u)]
        [TestCase(0x74797031u)]
        public void ReadsEverySfntFlavorWithoutRequiringOutlines(uint flavor) {
            byte[] font = Sfnt(flavor, ("name", Names(Name.Text(FamilyNameId, "Example"))), ("OS/2", Os2(0, "TEST")));
            FontMetadata metadata = FontMetadataReader.Read(font);
            var face = metadata.Faces.Single();
            using(Assert.EnterMultipleScope()) {
                Assert.That(metadata.Format, Is.EqualTo(FontFormat.Sfnt));
                Assert.That(face.SfntFlavor, Is.EqualTo(flavor));
                Assert.That(face.FamilyName, Is.EqualTo("Example"));
                Assert.That(face.VendorId, Is.EqualTo("TEST"));
                Assert.That(face.EmbeddingPermissions, Is.EqualTo(0));
                Assert.That(face.NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
                Assert.That(face.Os2TableStatus, Is.EqualTo(FontSectionStatus.Complete));
            }
        }
        [Test]
        public void ReadsAnIndependentlyWrittenHexVector() {
            // A one-table SFNT with one Windows family record, "A"; no fixture builder involved.
            byte[] font = Convert.FromHexString("0001000000010010000000006E616D65000000000000001C000000140000000100120003000104090001000200000041");
            Assert.That(FontMetadataReader.Read(font).Faces.Single().FamilyName, Is.EqualTo("A"));
        }
        [Test]
        public void PreservesAllRecordsAndSelectsPreferredFieldsWithoutChangingValues() {
            byte[] names = Names(Name.Text(FamilyNameId, "Legacy"), Name.Text(TypographicFamilyNameId, "  \0 Typographic \0 "),
                Name.Text(SubfamilyNameId, "Regular"), Name.Text(TypographicSubfamilyNameId, "Book"), Name.Text(FullNameId, "Full"), Name.Text(PostScriptNameId, "PS"),
                Name.Text(VersionNameId, "Version 1"), Name.Text(CopyrightNameId, "Copyright"), Name.Text(TrademarkNameId, "Trademark"),
                Name.Text(ManufacturerNameId, "Maker"), Name.Text(DesignerNameId, "Designer"), Name.Text(DescriptionNameId, "Description"),
                Name.Text(VendorUrlNameId, "vendor-url"), Name.Text(DesignerUrlNameId, "designer-url"), Name.Text(LicenseDescriptionNameId, "license-text"),
                Name.Text(LicenseUrlNameId, "license-url"), Name.Text(4096, "unknown-id"), Name.Text(FamilyNameId, "Legacy"), Name.Text(5000, ""));
            var face = FontMetadataReader.Read(Sfnt(("name", names))).Faces.Single();
            using(Assert.EnterMultipleScope()) {
                Assert.That(face.NameRecords.Count, Is.EqualTo(19));
                Assert.That(face.NameRecords[1].Value, Is.EqualTo("  \0 Typographic \0 "));
                Assert.That(face.NameRecords[16].NameId, Is.EqualTo(4096));
                Assert.That(face.NameRecords[18].Value, Is.Empty);
                Assert.That(face.NameRecords.Select(x => x.Index), Is.EqualTo(Enumerable.Range(0, 19)));
                Assert.That(new[] { face.FamilyName, face.SubfamilyName, face.FullName, face.PostScriptName, face.Version,
                    face.Copyright, face.Trademark, face.Manufacturer, face.Designer, face.Description,
                    face.VendorUrl, face.DesignerUrl, face.LicenseDescription, face.LicenseUrl },
                    Is.EqualTo([ "Typographic", "Book", "Full", "PS", "Version 1", "Copyright", "Trademark",
                        "Maker", "Designer", "Description", "vendor-url", "designer-url", "license-text", "license-url" ]));
            }
        }
        [TestCase("en-US")]
        [TestCase("tr-TR")]
        public void PreferenceIsIndependentOfCurrentCultureAndStableOnTies(string culture) {
            var previous = CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var font = Sfnt(("name", Names(Name.Text(FamilyNameId, "Unicode", 0, 4, 0), Name.Text(FamilyNameId, "French", language: 0x040c),
                    Name.Text(FamilyNameId, "English"), Name.Text(FamilyNameId, "Later English"))));
                Assert.That(FontMetadataReader.Read(font).Faces[0].FamilyName, Is.EqualTo("English"));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
        [Test]
        public void DecodesFormatOneLanguageTagsAndPrefersEnglish() {
            var names = NamesWithLanguages([Name.Text(FamilyNameId, "French", language: 0x8000), Name.Text(FamilyNameId, "English", language: 0x8001)], ["fr-FR", "en-US"]);
            var face = FontMetadataReader.Read(Sfnt(("name", names))).Faces[0];
            Assert.That(face.NameRecords.Select(x => x.LanguageTag), Is.EqualTo(["fr-FR", "en-US"]));
            Assert.That(face.FamilyName, Is.EqualTo("English"));
        }
        [TestCase(1, 0, "80", "Ä")]
        [TestCase(3, 2, "82A0", "あ")]
        [TestCase(3, 3, "D6D0", "中")]
        [TestCase(3, 4, "A4A4", "中")]
        [TestCase(3, 5, "B0A1", "가")]
        [TestCase(3, 6, "8861", "가")]
        public void DecodesMacRomanAndWindowsCjk(int platform, int encoding, string hex, string expected) {
            var name = new Name(FamilyNameId, Convert.FromHexString(hex), (ushort)platform, (ushort)encoding);
            Assert.That(FontMetadataReader.Read(Sfnt(("name", Names(name)))).Faces[0].FamilyName, Is.EqualTo(expected));
        }
        [Test]
        public void MissingTablesAreDistinctFromMissingNameIdsAndUnreadableTables() {
            var empty = FontMetadataReader.Read(Sfnt(("head", new byte[4]))).Faces[0];
            Assert.That(empty.NameTableStatus, Is.EqualTo(FontSectionStatus.Missing));
            Assert.That(empty.Os2TableStatus, Is.EqualTo(FontSectionStatus.Missing));
            Assert.That(empty.EmbeddingPermissions, Is.Null);
            var noFamily = FontMetadataReader.Read(Sfnt(("name", Names(Name.Text(LicenseDescriptionNameId, "license"))))).Faces[0];
            Assert.That(noFamily.FamilyName, Is.Null);
            Assert.That(noFamily.LicenseDescription, Is.EqualTo("license"));
            Assert.That(noFamily.NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(FontMetadataReader.Read(Sfnt(("name", Names()))).Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Complete));
        }
        [Test]
        public void MalformedNamePreservesOs2AndStrictReadCarriesEquivalentResult() {
            byte[] font = Sfnt(("name", new byte[] { 0, 0, 0 }), ("OS/2", Os2(8, "ACME")));
            var result = FontMetadataReader.Inspect(font);
            var face = result.Metadata!.Faces.Single();
            using(Assert.EnterMultipleScope()) {
                Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
                Assert.That(face.NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
                Assert.That(face.Os2TableStatus, Is.EqualTo(FontSectionStatus.Complete));
                Assert.That(face.EmbeddingPermissions, Is.EqualTo(8));
                Assert.That(face.VendorId, Is.EqualTo("ACME"));
                Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.InvalidNameTable));
                Assert.That(result.Diagnostics.Single().FaceIndex, Is.EqualTo(0));
                Assert.That(result.Diagnostics.Single().TableTag, Is.EqualTo("name"));
            }
            var exception = Assert.Throws<FontReadException>(() => FontMetadataReader.Read(font))!;
            Assert.That(exception.Result.Status, Is.EqualTo(result.Status));
            Assert.That(exception.Result.Metadata!.Faces[0].VendorId, Is.EqualTo("ACME"));
            Assert.That(exception.Result.Diagnostics.Select(x => x.Code), Is.EqualTo(result.Diagnostics.Select(x => x.Code)));
        }
        [TestCase(0, FontSectionStatus.Invalid, null)]
        [TestCase(9, FontSectionStatus.Invalid, null)]
        [TestCase(10, FontSectionStatus.Partial, 4)]
        [TestCase(61, FontSectionStatus.Partial, 4)]
        [TestCase(62, FontSectionStatus.Complete, 4)]
        public void Os2RecoveryPreservesIndependentlyReadFields(int length, FontSectionStatus expected, int? permission) {
            var result = FontMetadataReader.Inspect(Sfnt(("name", Names(Name.Text(FamilyNameId, "A"))), ("OS/2", Os2(4, length: length))));
            Assert.That(result.Metadata!.Faces[0].FamilyName, Is.EqualTo("A"));
            Assert.That(result.Metadata.Faces[0].Os2TableStatus, Is.EqualTo(expected));
            Assert.That(result.Metadata.Faces[0].EmbeddingPermissions, Is.EqualTo(permission));
            Assert.That(result.Status, Is.EqualTo(expected == FontSectionStatus.Complete ? FontReadStatus.Complete : FontReadStatus.Partial));
        }
        [Test]
        public void UnknownTableVersionsAreUnsupportedWithoutFabricatedFacts() {
            byte[] names = Names(Name.Text(FamilyNameId, "A"));
            U16(names, 0, 2);
            var result = FontMetadataReader.Inspect(Sfnt(("name", names), ("OS/2", Os2(version: 6))));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Unsupported));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Unsupported));
            Assert.That(result.Metadata.Faces[0].EmbeddingPermissions, Is.Null);
            Assert.Throws<FontFeatureNotSupportedException>(() => FontMetadataReader.Read(Sfnt(("name", names))));
        }
        [TestCase("D800")]
        [TestCase("0041FF")]
        public void InvalidUnicodeRecordDoesNotEraseOtherRecords(string hex) {
            var result = FontMetadataReader.Inspect(Sfnt(("name", Names(Name.Text(FamilyNameId, "A"), new Name(LicenseDescriptionNameId, Convert.FromHexString(hex)), Name.Text(LicenseUrlNameId, "url")))));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(result.Metadata.Faces[0].NameRecords.Select(x => x.Index), Is.EqualTo([0, 2]));
            Assert.That(result.Metadata.Faces[0].LicenseUrl, Is.EqualTo("url"));
        }
        [TestCase(false, FontReadStatus.Unsupported, FontSectionStatus.Unsupported)]
        [TestCase(true, FontReadStatus.Partial, FontSectionStatus.Partial)]
        public void UnknownEncodingsAreNotDecodedAsAscii(bool validRecord, FontReadStatus status, FontSectionStatus section) {
            var records = new List<Name> { new Name(FamilyNameId, [65], 1, 99) };
            if(validRecord)
                records.Add(Name.Text(LicenseDescriptionNameId, "license"));
            var result = FontMetadataReader.Inspect(Sfnt(("name", Names(records.ToArray()))));
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(section));
            Assert.That(result.Metadata.Faces[0].FamilyName, Is.Null);
            Assert.That(result.Diagnostics[0].Category, Is.EqualTo(FontDiagnosticCategory.UnsupportedEncoding));
        }
        [Test]
        public void InvalidStringRangeAndLanguageReferencePreserveSafeRecordValues() {
            byte[] names = Names(Name.Text(FamilyNameId, "A"), Name.Text(LicenseDescriptionNameId, "B"), Name.Text(LicenseUrlNameId, "C", language: 0x8000));
            U16(names, NameRecordPosition(1) + NameStringOffsetField, 65535);
            var result = FontMetadataReader.Inspect(Sfnt(("name", names)));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].NameRecords.Select(x => x.Value), Is.EqualTo(["A", "C"]));
            Assert.That(result.Metadata.Faces[0].NameRecords[1].LanguageTag, Is.Null);
        }
        [Test]
        public void InvalidMetadataRangeDoesNotDiscardOtherTables() {
            byte[] font = Example();
            U32(font, TableOffsetPosition(1), uint.MaxValue); // name record's offset, after the OS/2 record.
            var result = FontMetadataReader.Inspect(font);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(result.Metadata.Faces[0].VendorId, Is.EqualTo("TEST"));
        }
        [Test]
        public void DuplicateNameTagIsAmbiguousButOs2Survives() {
            var result = FontMetadataReader.Inspect(Sfnt(("name", Names(Name.Text(FamilyNameId, "A"))), ("name", Names(Name.Text(FamilyNameId, "B"))), ("OS/2", Os2())));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(result.Metadata.Faces[0].FamilyName, Is.Null);
            Assert.That(result.Metadata.Faces[0].VendorId, Is.EqualTo("TEST"));
        }
        [Test]
        public void OverlappingTablesAreNotParsedAsIndependentFacts() {
            byte[] font = Example();
            U32(font, TableOffsetPosition(1), (uint)ReadOffset(font, TableOffsetPosition(0)));
            var result = FontMetadataReader.Inspect(font);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(result.Metadata!.Faces[0].NameTableStatus, Is.EqualTo(FontSectionStatus.Invalid));
            Assert.That(result.Metadata.Faces[0].Os2TableStatus, Is.EqualTo(FontSectionStatus.Invalid));
        }
        [Test]
        public void UnsortedUniqueTagsProduceWarningWithoutLosingMetadata() {
            byte[] font = Example();
            var first = font.AsSpan(12, 16).ToArray();
            font.AsSpan(28, 16).CopyTo(font.AsSpan(12));
            first.CopyTo(font, 28);
            var result = FontMetadataReader.Inspect(font);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete));
            Assert.That(result.Diagnostics.Single().Severity, Is.EqualTo(FontDiagnosticSeverity.Warning));
            Assert.That(result.Metadata!.Faces[0].FamilyName, Is.EqualTo("Example"));
        }
    }
}
