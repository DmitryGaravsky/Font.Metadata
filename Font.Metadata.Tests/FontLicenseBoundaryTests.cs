using System;
using System.Linq;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;
using static Fonts.Metadata.Tests.FontLicenseRuleTests;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class FontLicenseBoundaryTests {
        [TestCase("Apache License 2.0\nis mentioned only as an example")]
        [TestCase("Apache License 2.0\nis not the license of this font")]
        [TestCase("SIL Open Font License\nVersion 1.1\nis not applicable here")]
        [TestCase("BSD 3-Clause License\nfor comparison only")]
        [TestCase("Apache License 2.0\nWITH: Custom-exception")]
        [TestCase("Apache License 2.0\nOR; MIT")]
        [TestCase("Apache License 2.0\n+")]
        [TestCase("SIL Open Font License\nVersion 1.1\nAND-Invalid")]
        [TestCase("Example: \u00abfirst; Licensed under MIT; last\u00bb")]
        [TestCase("Example: \u201efirst; Licensed under MIT; last\u201c")]
        [TestCase("Example: \u2039first; Licensed under MIT; last\u203a")]
        [TestCase("Example: \u0060first; Licensed under MIT; last\u0060")]
        [TestCase("Example: \u201afirst; Licensed under MIT; last\u2018")]
        [TestCase("SPDX-License-Identifier: MIT\nOR Apache-2.0")]
        [TestCase("SPDX-License-Identifier: MIT\nWITH Unknown-exception")]
        [TestCase("SPDX-License-Identifier: MIT\nis mentioned as an example")]
        [TestCase("Not\nSPDX-License-Identifier: MIT\nOther text")]
        [TestCase("Example:\nSPDX-License-Identifier: MIT\nOther text")]
        [TestCase("SPDX-License-Identifier: Unknown-License\nCopyright Example")]
        [TestCase("Example: \u00abfirst;\nSPDX-License-Identifier: MIT\nlast\u00bb")]
        [TestCase("Example: \u00abouter \u00abinner\u00bb; Licensed under MIT; outer\u00bb")]
        [TestCase("Example: \u201couter \u201cinner\u201d; Licensed under MIT; outer\u201d")]
        public void ContinuedTitlesAndQuotedStatementsDoNotBecomeEvidence(string value) {
            var face = ReadFace(Name.Text(LicenseDescriptionNameId, value));
            var result = FontLicenseAnalyzer.Analyze(face);
            Assert.That(result.Hints, Is.Empty);
            Assert.That(result.IsSourceComplete, Is.True);
            Assert.That(face.NameRecords[0].Value, Is.EqualTo(value));
        }
        [TestCase("SPDX-License-Identifier: MIT\nCopyright Example", "MIT", "FLA001")]
        [TestCase("SPDX-License-Identifier:\tmit\r\nCopyright Example", "MIT", "FLA001")]
        [TestCase("Notes about this font.\nSPDX-License-Identifier: MIT\nCopyright Example", "MIT", "FLA001")]
        [TestCase("Apache License 2.0\nOR; Licensed under MIT", "MIT", "FLA001")]
        [TestCase("Example: \u00abhttps://example.test/; Licensed under Apache-2.0\u00bb; Licensed under MIT", "MIT", "FLA001")]
        [TestCase("Example: \u201efirst; Licensed under Apache-2.0\u201c; Licensed under MIT", "MIT", "FLA001")]
        [TestCase("Example: \u0060first; Licensed under Apache-2.0\u0060; Licensed under MIT", "MIT", "FLA001")]
        public void IndependentStatementsSurviveRejectedContext(string value, string identifier, string rule) {
            var face = ReadFace(Name.Text(LicenseDescriptionNameId, value));
            var result = FontLicenseAnalyzer.Analyze(face);
            Assert.That(result.Hints, Has.Count.EqualTo(1));
            var hint = result.Hints[0];
            Assert.That((hint.Identifier, hint.IdentifierKind, hint.RuleId),
                Is.EqualTo((identifier, FontLicenseIdentifierKind.Spdx, rule)));
            Assert.That(hint.SourceRecord, Is.SameAs(face.NameRecords[0]));
        }
        [Test]
        public void DeepQuotesDoNotExposeDeclarationsAndPreserveOutsideEvidence() {
            string quoted = new string('\u00ab', 10_000) + "example" + new string('\u00bb', 9_999);
            string value = quoted + "; Licensed under Apache-2.0; end\u00bb; Licensed under MIT";
            var face = ReadFace(Name.Text(LicenseDescriptionNameId, value));
            var result = FontLicenseAnalyzer.Analyze(face);
            Assert.That(result.Hints.Select(h => (h.Identifier, h.RuleId)),
                Is.EqualTo(new[] { ("MIT", "FLA001") }));
            Assert.That(result.Hints[0].SourceRecord, Is.SameAs(face.NameRecords[0]));
        }
        [Test]
        public void ManyNewlinesDoNotTurnProseIntoAnAnnotation() {
            string value = "Not" + new string('\n', 10_000) +
                "SPDX-License-Identifier: Apache-2.0; Licensed under MIT";
            var face = ReadFace(Name.Text(LicenseDescriptionNameId, value));
            var result = FontLicenseAnalyzer.Analyze(face);
            Assert.That(result.Hints.Select(h => (h.Identifier, h.RuleId)),
                Is.EqualTo(new[] { ("MIT", "FLA001") }));
        }
        [Test]
        public void SeparateAnnotationLinesPreserveSourceAndOrder() {
            var face = ReadFace(Name.Text(LicenseDescriptionNameId,
                "SPDX-License-Identifier: MIT\nSPDX-License-Identifier: Apache-2.0\nCopyright Example"));
            var result = FontLicenseAnalyzer.Analyze(face);
            Assert.That(result.Hints.Select(h => (h.Identifier, h.RuleId)),
                Is.EqualTo(new[] { ("MIT", "FLA001"), ("Apache-2.0", "FLA001") }));
            Assert.That(result.Hints.All(h => ReferenceEquals(h.SourceRecord, face.NameRecords[0])), Is.True);
        }
    }
}
