using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;
using static Fonts.Metadata.Tests.FontLicenseRuleTests;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class FontLicenseContractTests {
        const string AllocationFailureMessage = "Repeated shared license text allocated {0} bytes.";
        [Test]
        public void NullFaceIsRejected() {
            Assert.Throws<ArgumentNullException>(() => FontLicenseAnalyzer.Analyze(null!));
        }
        [TestCase(FontSectionStatus.Complete, true)]
        [TestCase(FontSectionStatus.Missing, true)]
        [TestCase(FontSectionStatus.Partial, false)]
        [TestCase(FontSectionStatus.Invalid, false)]
        [TestCase(FontSectionStatus.NotRead, false)]
        [TestCase(FontSectionStatus.Unsupported, false)]
        public void CompletenessDependsOnlyOnNameStatus(FontSectionStatus status, bool complete) {
            // Internal construction isolates all section states without requiring contradictory wire fixtures.
            var face = new FontMetadata.Face(0, null, status);
            var result = FontLicenseAnalyzer.Analyze(face);
            Assert.That(result.IsSourceComplete, Is.EqualTo(complete));
            Assert.That(result.Hints, Is.Empty);
            AssertImmutable(result);
        }
        [Test]
        public void MissingOrUnrelatedRecordsDoNotAssertALicense() {
            byte[] missingData = Sfnt(("OS/2", Os2(ushort.MaxValue)));
            var missingFont = FontMetadataReader.Read(missingData);
            var missing = missingFont.Faces[0];
            var missingResult = FontLicenseAnalyzer.Analyze(missing);
            Assert.That(missing.NameTableStatus, Is.EqualTo(FontSectionStatus.Missing));
            Assert.That(missingResult.IsSourceComplete, Is.True);
            Assert.That(missingResult.Hints, Is.Empty);
            var records = new ushort[] { 0, 1, 10, 11, 12, 99 }.Select(id => Name.Text(id, "MIT")).ToArray();
            byte[] unrelatedData = Sfnt(("name", Names(records)), ("OS/2", Os2(0)));
            var unrelatedFont = FontMetadataReader.Read(unrelatedData);
            var unrelatedResult = FontLicenseAnalyzer.Analyze(unrelatedFont.Faces[0]);
            Assert.That(unrelatedResult.Hints, Is.Empty);
            Assert.That(unrelatedResult.IsSourceComplete, Is.True);
        }
        [Test]
        public void PartialReadRetainsOriginalIndexesRecordsAndDiagnostics() {
            byte[] data = Sfnt(("name", Names(
                new Name(LicenseDescriptionNameId, [0xd8, 0]),
                Name.Text(LicenseDescriptionNameId, "MIT"),
                Name.Text(LicenseDescriptionNameId, "MIT", language: 0x0411),
                Name.Text(LicenseUrlNameId, "https://www.apache.org/licenses/LICENSE-2.0"))));
            var inspection = FontMetadataReader.Inspect(data);
            var face = inspection.Metadata!.Faces[0];
            var records = face.NameRecords.ToArray();
            var diagnostics = inspection.Diagnostics.ToArray();
            var result = FontLicenseAnalyzer.Analyze(face);
            using(Assert.EnterMultipleScope()) {
                Assert.That(inspection.Status, Is.EqualTo(FontReadStatus.Partial));
                Assert.That(result.IsSourceComplete, Is.False);
                Assert.That(result.Hints.Select(h => h.SourceRecord.Index), Is.EqualTo(new[] { 1, 2, 3 }));
                Assert.That(result.Hints.Select(h => h.Identifier), Is.EqualTo(new[] { "MIT", "MIT", "Apache-2.0" }));
                Assert.That(face.NameRecords, Is.EqualTo(records));
                Assert.That(inspection.Diagnostics, Is.EqualTo(diagnostics));
            }
            for(int i = 0; i < records.Length; i++)
                Assert.That(result.Hints[i].SourceRecord, Is.SameAs(records[i]));
            AssertImmutable(result);
        }
        [Test]
        public void DamagedOs2DoesNotMakeCompleteNameSourceIncomplete() {
            byte[] data = Sfnt(("name", Names(Name.Text(LicenseDescriptionNameId, "MIT"))), ("OS/2", new byte[1]));
            var inspection = FontMetadataReader.Inspect(data);
            var result = FontLicenseAnalyzer.Analyze(inspection.Metadata!.Faces[0]);
            Assert.That(inspection.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(result.IsSourceComplete, Is.True);
            Assert.That(result.Hints.Single().Identifier, Is.EqualTo("MIT"));
        }
        [Test]
        public void NonPreferredAndUnreadableLanguageRecordsAreAnalyzed() {
            byte[] names = NamesWithLanguages([
                Name.Text(LicenseDescriptionNameId, "Unknown terms"),
                Name.Text(LicenseDescriptionNameId, "MIT", language: 0x0419),
                Name.Text(LicenseDescriptionNameId, "Apache-2.0", language: 0x0411),
                Name.Text(LicenseDescriptionNameId, "BSD-3-Clause", language: 0x8001)
            ], ["en-US"]);
            byte[] data = Sfnt(("name", names));
            var inspection = FontMetadataReader.Inspect(data);
            var face = inspection.Metadata!.Faces[0];
            var result = FontLicenseAnalyzer.Analyze(face);
            Assert.That(face.LicenseDescription, Is.EqualTo("Unknown terms"));
            Assert.That(face.NameRecords[3].LanguageTag, Is.Null);
            Assert.That(result.Hints.Select(h => h.Identifier), Is.EqualTo(new[] { "MIT", "Apache-2.0", "BSD-3-Clause" }));
            Assert.That(result.IsSourceComplete, Is.False);
            for(int i = 0; i < result.Hints.Count; i++)
                Assert.That(result.Hints[i].SourceRecord, Is.SameAs(face.NameRecords[i + 1]));
        }
        [Test]
        public void RecordOrderPrecedesRuleOrderAndConflictsAreNotResolved() {
            var face = ReadFace(
                Name.Text(LicenseUrlNameId, "https://spdx.org/licenses/MIT.html"),
                Name.Text(LicenseDescriptionNameId, "Licensed under Apache-2.0; Licensed under MIT"),
                Name.Text(LicenseDescriptionNameId, "Apache License"));
            var result = FontLicenseAnalyzer.Analyze(face);
            Assert.That(result.Hints.Select(h => (h.Identifier, h.RuleId, h.SourceRecord.Index)),
                Is.EqualTo(new[] { ("MIT", "FLA004", 0), ("Apache-2.0", "FLA001", 1), ("MIT", "FLA001", 1), ("Apache", "FLA003", 2) }));
        }
        [Test]
        public void RulesSortWithinARecordAndDeduplicateOnlyIdenticalEvidenceRules() {
            var face = ReadFace(Name.Text(LicenseDescriptionNameId,
                "License: https://opensource.org/licenses/MIT; Licensed under the MIT License; " +
                "Licensed under MIT; Licensed under Apache-2.0; Licensed under MIT"));
            var result = FontLicenseAnalyzer.Analyze(face);
            Assert.That(result.Hints.Select(h => (h.Identifier, h.RuleId)),
                Is.EqualTo(new[] { ("MIT", "FLA001"), ("Apache-2.0", "FLA001"), ("MIT", "FLA002"), ("MIT", "FLA004") }));
            Assert.That(result.Hints.All(h => ReferenceEquals(h.SourceRecord, face.NameRecords[0])), Is.True);
        }
        [Test]
        public void DifferentRecordsRetainEqualIdentifiersAndIndependentUrls() {
            var face = ReadFace(Name.Text(LicenseDescriptionNameId, "Licensed under MIT"),
                Name.Text(LicenseUrlNameId, "https://opensource.org/licenses/MIT"));
            var result = FontLicenseAnalyzer.Analyze(face);
            Assert.That(result.Hints.Select(h => h.RuleId), Is.EqualTo(new[] { "FLA001", "FLA004" }));
            Assert.That(result.Hints.Select(h => h.SourceRecord.Index), Is.EqualTo(new[] { 0, 1 }));
        }
        [Test]
        public void TextAndUrlCannotCompleteEachOthersKeyword() {
            var face = ReadFace(Name.Text(LicenseDescriptionNameId, "See the MIT"),
                Name.Text(LicenseUrlNameId, "LICENSE for terms"));
            Assert.That(FontLicenseAnalyzer.Analyze(face).Hints, Is.Empty);
        }
        [TestCase("MIT AND GPL-3.0-only")]
        [TestCase("MIT OR")]
        public void RejectedDeclarationDoesNotSuppressIndependentRecords(string text) {
            var face = ReadFace(Name.Text(LicenseDescriptionNameId, text),
                Name.Text(LicenseUrlNameId, "https://opensource.org/licenses/MIT"),
                Name.Text(LicenseDescriptionNameId, "Licensed under Apache-2.0"));
            var result = FontLicenseAnalyzer.Analyze(face);
            Assert.That(result.Hints.Select(h => (h.Identifier, h.RuleId, h.SourceRecord.Index)),
                Is.EqualTo(new[] { ("MIT", "FLA004", 1), ("Apache-2.0", "FLA001", 2) }));
            Assert.That(face.NameRecords[0].Value, Is.EqualTo(text));
        }
        [Test]
        public void ResultsAreImmutableAndIndependentAcrossInvocations() {
            var face = ReadFace(Name.Text(LicenseDescriptionNameId, "MIT"));
            var first = FontLicenseAnalyzer.Analyze(face);
            var second = FontLicenseAnalyzer.Analyze(face);
            AssertImmutable(first);
            Assert.That(second.Hints, Is.Not.SameAs(first.Hints));
            Assert.That(second.Hints.Single().SourceRecord, Is.SameAs(first.Hints.Single().SourceRecord));
            foreach(Type type in new[] { typeof(FontLicenseAnalysis), typeof(FontLicenseHint) }) {
                Assert.That(type.IsSealed, Is.True);
                Assert.That(type.GetConstructors(), Is.Empty);
                Assert.That(type.GetProperties().All(p => p.SetMethod == null), Is.True);
            }
        }
        [Test]
        public void ResultsDoNotDependOnCultureOrParallelInvocations() {
            var face = ReadFace(Name.Text(LicenseDescriptionNameId, "Licensed under mit; Licensed under the MIT License"),
                Name.Text(LicenseUrlNameId, "HTTPS://WWW.APACHE.ORG/licenses/LICENSE-2.0"));
            var other = ReadFace(Name.Text(LicenseDescriptionNameId, "BSD License"));
            var expected = new[] { ("MIT", FontLicenseIdentifierKind.Spdx, "FLA001", 0),
                ("MIT", FontLicenseIdentifierKind.Spdx, "FLA002", 0), ("Apache-2.0", FontLicenseIdentifierKind.Spdx, "FLA004", 1) };
            Parallel.For(0, 24, i => {
                var old = CultureInfo.CurrentCulture;
                var oldUi = CultureInfo.CurrentUICulture;
                try {
                    var culture = CultureInfo.GetCultureInfo(new[] { "", "en-US", "tr-TR" }[i % 3]);
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = culture;
                    var result = FontLicenseAnalyzer.Analyze(face);
                    Assert.That(result.Hints.Select(h => (h.Identifier, h.IdentifierKind, h.RuleId, h.SourceRecord.Index)), Is.EqualTo(expected));
                    Assert.That(result.Hints.All(h => ReferenceEquals(h.SourceRecord, face.NameRecords[h.SourceRecord.Index])), Is.True);
                    Assert.That(FontLicenseAnalyzer.Analyze(other).Hints.Single().Identifier, Is.EqualTo("BSD"));
                }
                finally {
                    CultureInfo.CurrentCulture = old;
                    CultureInfo.CurrentUICulture = oldUi;
                }
            });
        }
        [Test]
        public void DeepExpressionsAndLongStringsDoNotRequireRecursiveParsing() {
            string[] values = {
                new string('(', 10000) + "MIT OR Apache-2.0" + new string(')', 10000),
                string.Join(" AND ", Enumerable.Repeat("MIT", 10000)),
                new string('x', 200000)
            };
            foreach(string value in values) {
                // Bypass wire string-size limits only for this analyzer-only stress scenario.
                var record = new FontMetadata.NameRecord(0, 13, 3, 1, 0x0409, null, value);
                var records = Array.AsReadOnly(new[] { record });
                var face = new FontMetadata.Face(0, null, FontSectionStatus.Complete, records: records);
                var result = FontLicenseAnalyzer.Analyze(face);
                Assert.That(result.Hints, Is.Empty);
                Assert.That(result.IsSourceComplete, Is.True);
            }
        }
        [Test]
        public void SharedDecodedTextReusesMatchingWorkWithoutSharingHintCollections() {
            const int count = 1000;
            string value = new string(' ', 20000) + "MIT";
            var records = Enumerable.Range(0, count)
                .Select(i => new FontMetadata.NameRecord(i, 13, 3, 1, 0x0409, null, value)).ToArray();
            var snapshot = Array.AsReadOnly(records);
            var face = new FontMetadata.Face(0, null, FontSectionStatus.Complete, records: snapshot);
            FontLicenseAnalyzer.Analyze(face);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var result = FontLicenseAnalyzer.Analyze(face);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(result.Hints, Has.Count.EqualTo(count));
            Assert.That(allocated, Is.LessThan(2_000_000), string.Format(CultureInfo.InvariantCulture, AllocationFailureMessage, allocated));
            for(int i = 0; i < count; i++)
                Assert.That(result.Hints[i].SourceRecord, Is.SameAs(records[i]));
        }
        [Test]
        public void SharedTtcNameTablesRetainEachFacesRecordEvidence() {
            byte[] names = Names(Name.Text(LicenseDescriptionNameId, "MIT"));
            byte[] data = Collection([("name", names)], [("name", names)]);
            int first = ReadOffset(data, CollectionFacePosition(0)), second = ReadOffset(data, CollectionFacePosition(1));
            U32(data, second + TableOffsetPosition(0), (uint)ReadOffset(data, first + TableOffsetPosition(0)));
            var metadata = FontMetadataReader.Read(data);
            var firstResult = FontLicenseAnalyzer.Analyze(metadata.Faces[0]);
            var secondResult = FontLicenseAnalyzer.Analyze(metadata.Faces[1]);
            Assert.That(firstResult.Hints[0].SourceRecord, Is.SameAs(metadata.Faces[0].NameRecords[0]));
            Assert.That(secondResult.Hints[0].SourceRecord, Is.SameAs(metadata.Faces[1].NameRecords[0]));
            Assert.That(firstResult.Hints, Is.Not.SameAs(secondResult.Hints));
            AssertImmutable(firstResult);
            AssertImmutable(secondResult);
        }
        static void AssertImmutable(FontLicenseAnalysis result) {
            Assert.That(result.Hints, Is.Not.InstanceOf<FontLicenseHint[]>());
            Assert.That(result.Hints, Is.Not.InstanceOf<List<FontLicenseHint>>());
            if(result.Hints is IList<FontLicenseHint> generic) {
                Assert.That(generic.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => generic.Clear());
            }
            if(result.Hints is IList list) {
                Assert.That(list.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => list.Clear());
                if(list.Count > 0)
                    Assert.Throws<NotSupportedException>(() => list[0] = list[0]);
            }
        }
    }
}
