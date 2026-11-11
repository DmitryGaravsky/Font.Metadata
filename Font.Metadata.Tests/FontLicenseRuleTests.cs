using System;
using System.Collections.Generic;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class FontLicenseRuleTests {
        // Independent expected vocabulary; tests do not enumerate the production rule tables.
        static readonly string[] identifiers = {
            "MIT", "Apache-2.0", "OFL-1.0", "OFL-1.1", "BSD-2-Clause", "BSD-3-Clause", "MPL-2.0", "BSL-1.0",
            "GPL-2.0-only", "GPL-2.0-or-later", "GPL-3.0-only", "GPL-3.0-or-later",
            "LGPL-2.1-only", "LGPL-2.1-or-later", "LGPL-3.0-only", "LGPL-3.0-or-later"
        };
        static IEnumerable<TestCaseData> IdentifierCases() {
            foreach(string id in identifiers) {
                foreach(string value in new[] { id, id.ToLowerInvariant(), id.ToUpperInvariant() }) {
                    foreach(ushort nameId in new ushort[] { 13, 14 })
                        yield return new TestCaseData(value, id, nameId);
                }
            }
        }
        [TestCaseSource(nameof(IdentifierCases))]
        public void ExactIdentifiersUseCanonicalSpelling(string input, string expected, ushort nameId) {
            AssertSingle(input, expected, FontLicenseIdentifierKind.Spdx, "FLA001", nameId);
        }
        [TestCase("MIT License", "MIT", "FLA002")]
        [TestCase("The MIT License", "MIT", "FLA002")]
        [TestCase("Apache License, Version 2.0", "Apache-2.0", "FLA002")]
        [TestCase("Apache License Version 2.0", "Apache-2.0", "FLA002")]
        [TestCase("Apache License 2.0", "Apache-2.0", "FLA002")]
        [TestCase("Apache 2.0", "Apache-2.0", "FLA002")]
        [TestCase("Apache License", "Apache", "FLA003")]
        [TestCase("SIL Open Font License, Version 1.0", "OFL-1.0", "FLA002")]
        [TestCase("SIL Open Font License, Version 1.1", "OFL-1.1", "FLA002")]
        [TestCase("SIL Open Font License", "OFL", "FLA003")]
        [TestCase("Open Font License", "OFL", "FLA003")]
        [TestCase("BSD 2-Clause License", "BSD-2-Clause", "FLA002")]
        [TestCase("BSD 3-Clause License", "BSD-3-Clause", "FLA002")]
        [TestCase("BSD License", "BSD", "FLA003")]
        [TestCase("GNU General Public License", "GPL", "FLA003")]
        [TestCase("GNU General Public License version 2", "GPL", "FLA003")]
        [TestCase("GPL", "GPL", "FLA003")]
        [TestCase("GPL version 3", "GPL", "FLA003")]
        [TestCase("GNU Lesser General Public License", "LGPL", "FLA003")]
        [TestCase("GNU Lesser General Public License version 2.1", "LGPL", "FLA003")]
        [TestCase("LGPL", "LGPL", "FLA003")]
        [TestCase("Mozilla Public License 2.0", "MPL-2.0", "FLA002")]
        [TestCase("Mozilla Public License Version 2.0", "MPL-2.0", "FLA002")]
        [TestCase("Mozilla Public License", "MPL", "FLA003")]
        [TestCase("Boost Software License 1.0", "BSL-1.0", "FLA002")]
        [TestCase("Boost Software License - Version 1.0", "BSL-1.0", "FLA002")]
        [TestCase("Boost Software License", "Boost", "FLA003")]
        public void NamesRequireExactSupportedVersionAndVariant(string name, string expected, string rule) {
            var kind = rule == "FLA003" ? FontLicenseIdentifierKind.Family : FontLicenseIdentifierKind.Spdx;
            foreach(string prefix in new[] { "", "Licensed under ", "Licensed under the ",
                "This font is licensed under ", "This Font Software is licensed under the " }) {
                AssertSingle(prefix + name, expected, kind, rule);
            }
        }
        [TestCase("SPDX-License-Identifier: MIT", "MIT", "FLA001")]
        [TestCase("Licensed under MIT.", "MIT", "FLA001")]
        [TestCase("This Font Software is licensed under the SIL Open Font License, Version 1.1.", "OFL-1.1", "FLA002")]
        [TestCase("SIL OPEN FONT LICENSE\nVersion 1.1", "OFL-1.1", "FLA002")]
        [TestCase("SIL OPEN FONT LICENSE\r\nVersion 1.0\r\nPREAMBLE\nSynthetic body.", "OFL-1.0", "FLA002")]
        [TestCase("Apache License\nVersion 2.0\nTERMS AND CONDITIONS", "Apache-2.0", "FLA002")]
        [TestCase("Mozilla Public License Version 2.0\nSynthetic body.", "MPL-2.0", "FLA002")]
        [TestCase("Boost Software License\nVersion 1.0\nSynthetic body.", "BSL-1.0", "FLA002")]
        [TestCase("\0 \tLicensed\r\nunder\u00a0the MIT License \0", "MIT", "FLA002")]
        public void ExplicitStatementsAndVersionedHeadersAreRecognized(string input, string expected, string rule) {
            AssertSingle(input, expected, FontLicenseIdentifierKind.Spdx, rule);
        }
        [TestCase("")]
        [TestCase(" \0 ")]
        [TestCase("Not licensed under the MIT License")]
        [TestCase("This font is not licensed under MIT")]
        [TestCase("This font is NOT licensed under the Apache License, Version 2.0")]
        [TestCase("Licensed under MIT?")]
        [TestCase("Licensed under MIT!")]
        [TestCase("Compatible with the MIT License")]
        [TestCase("See the MIT License for comparison")]
        [TestCase("The documentation mentions Apache License 2.0")]
        [TestCase("Example: \"Licensed under the MIT License\"")]
        [TestCase("Not\nlicensed under MIT")]
        [TestCase("XMIT")]
        [TestCase("MIT-like")]
        [TestCase("MIT-0")]
        [TestCase("LicenseRef-company-MIT")]
        [TestCase("\u041cIT")]
        [TestCase("Permission is hereby granted")]
        [TestCase("Redistribution and use in source and binary forms are permitted")]
        [TestCase("MI\0T")]
        [TestCase("MIT\0License")]
        [TestCase("Apache License 9.0")]
        [TestCase("SIL Open Font License 9.0")]
        [TestCase("BSD 4-Clause License")]
        [TestCase("Mozilla Public License 1.1")]
        [TestCase("OFL-9.0")]
        [TestCase("Definitely-Not-A-License")]
        [TestCase("ISC")]
        [TestCase("Этот шрифт не лицензирован по MIT")]
        [TestCase("MIT License is mentioned only as an example")]
        [TestCase("MIT License\nis mentioned only as an example")]
        [TestCase("Apache License\nVersion 9.0")]
        [TestCase("Apache License 2.0\nOR MIT")]
        [TestCase("Apache License 2.0\nOR(MIT)")]
        [TestCase("SIL Open Font License\nVersion 1.1 OR MIT")]
        [TestCase("SIL Open Font License\nVersion 1.1\nWITH Unknown-exception")]
        [TestCase("MIT\nOR Apache-2.0")]
        [TestCase("Example: \"unrelated; Licensed under MIT; unrelated\"")]
        [TestCase("Example: 'unrelated; Licensed under MIT; unrelated'")]
        [TestCase("Example: \u201cunrelated; Licensed under MIT; unrelated\u201d")]
        [TestCase("(unrelated; Licensed under MIT)")]
        public void MentionsNegationsAndUnknownVariantsDoNotBecomeEvidence(string input) {
            Assert.That(Analyze(input).Hints, Is.Empty);
        }
        [TestCase("Not MIT; Licensed under the Apache License, Version 2.0", "Apache-2.0", "FLA002")]
        [TestCase("Licensed under MIT; not licensed under Apache-2.0", "MIT", "FLA001")]
        [TestCase("Not MIT. Licensed under Apache-2.0.", "Apache-2.0", "FLA001")]
        [TestCase("Example: \"https://example.test/\"; Licensed under MIT", "MIT", "FLA001")]
        [TestCase("Not licensed under MIT? Licensed under Apache-2.0.", "Apache-2.0", "FLA001")]
        [TestCase("MIT OR Apache-2.0; Licensed under BSD-3-Clause", "BSD-3-Clause", "FLA001")]
        [TestCase("Licensed under MIT; Licensed under MIT", "MIT", "FLA001")]
        public void IndependentStatementsSurviveLocalRejectionAndDuplicates(string input, string id, string rule) {
            AssertSingle(input, id, FontLicenseIdentifierKind.Spdx, rule);
        }
        [TestCase("MIT OR Apache-2.0")]
        [TestCase("MIT AND Apache-2.0")]
        [TestCase("(MIT OR Apache-2.0) AND BSD-3-Clause")]
        [TestCase("SPDX-License-Identifier: MIT OR Apache-2.0")]
        [TestCase("Licensed under MIT OR Apache-2.0")]
        [TestCase("GPL-2.0-only WITH Classpath-exception-2.0")]
        [TestCase("MIT WITH Unknown-exception")]
        [TestCase("MIT OR")]
        [TestCase("MIT WITH")]
        [TestCase("MIT OR (Apache-2.0")]
        [TestCase("MIT OR OR Apache-2.0")]
        [TestCase("MIT or Apache-2.0")]
        [TestCase("MIT and Apache-2.0")]
        [TestCase("MIT with Unknown-exception")]
        [TestCase("MIT / Apache-2.0")]
        [TestCase("MIT & Apache-2.0")]
        [TestCase("MIT, Apache-2.0")]
        [TestCase("LicenseRef-vendor-custom")]
        [TestCase("AdditionRef-custom")]
        [TestCase("DocumentRef-doc:LicenseRef-custom")]
        [TestCase("GPL-2.0-with-classpath-exception")]
        [TestCase("GPL-2.0")]
        [TestCase("GPL-2.0+")]
        [TestCase("SEE LICENSE IN MIT")]
        [TestCase("SEE LICENSE IN LICENSE.txt")]
        [TestCase("Terms AND Conditions")]
        [TestCase("Custom OR Commercial")]
        [TestCase("Terms WITH MIT")]
        [TestCase("(MIT)")]
        public void ExpressionsAreNeverFlattenedToIndividualHints(string input) {
            var analysis = Analyze(input);
            Assert.That(analysis.Hints, Is.Empty);
            Assert.That(analysis.IsSourceComplete, Is.True);
        }
        static IEnumerable<TestCaseData> UrlCases() {
            var urls = new List<(string Url, string Id, FontLicenseIdentifierKind Kind)>();
            foreach(string host in new[] { "apache.org", "www.apache.org" }) {
                foreach(string path in new[] { "/licenses/LICENSE-2.0", "/licenses/LICENSE-2.0.txt" })
                    urls.Add((host + path, "Apache-2.0", FontLicenseIdentifierKind.Spdx));
            }
            foreach(string host in new[] { "opensource.org", "www.opensource.org" }) {
                foreach(string path in new[] { "/licenses/MIT", "/licenses/mit-license.php", "/license/mit" })
                    urls.Add((host + path, "MIT", FontLicenseIdentifierKind.Spdx));
            }
            urls.Add(("scripts.sil.org/OFL", "OFL", FontLicenseIdentifierKind.Family));
            urls.Add(("scripts.sil.org/OFL/", "OFL", FontLicenseIdentifierKind.Family));
            foreach(string id in identifiers)
                urls.Add(("spdx.org/licenses/" + id + ".html", id, FontLicenseIdentifierKind.Spdx));
            foreach(var url in urls) {
                foreach(string scheme in new[] { "http://", "https://" }) {
                    foreach(ushort nameId in new ushort[] { 13, 14 })
                        yield return new TestCaseData(scheme + url.Url, url.Id, url.Kind, nameId);
                }
            }
        }
        [TestCaseSource(nameof(UrlCases))]
        public void EveryAllowedUrlIsIndependentEvidence(string input, string id, FontLicenseIdentifierKind kind, ushort nameId) {
            AssertSingle(input, id, kind, "FLA004", nameId);
        }
        [TestCase("HTTPS://WWW.APACHE.ORG/licenses/LICENSE-2.0")]
        [TestCase("https://www.apache.org:443/licenses/LICENSE-2.0")]
        [TestCase("http://www.apache.org:80/licenses/LICENSE-2.0")]
        [TestCase("License: https://www.apache.org/licenses/LICENSE-2.0")]
        [TestCase("Licensed under https://www.apache.org/licenses/LICENSE-2.0")]
        [TestCase("\0 https://www.apache.org/licenses/LICENSE-2.0 \0")]
        public void AllowedUrlContextAndAuthorityFormsAreRecognized(string input) {
            AssertSingle(input, "Apache-2.0", FontLicenseIdentifierKind.Spdx, "FLA004");
        }
        [TestCase("https://www.apache.org/licenses/license-2.0")]
        [TestCase("https://apache.org.evil.test/licenses/LICENSE-2.0")]
        [TestCase("https://evil.test/apache.org/licenses/LICENSE-2.0")]
        [TestCase("https://evil.test/?license=https://www.apache.org/licenses/LICENSE-2.0")]
        [TestCase("https://evil.test/?name=MIT%20License")]
        [TestCase("https://evil.test/?name=unrelated;Licensed under MIT")]
        [TestCase("https://www.apache.org@evil.test/licenses/LICENSE-2.0")]
        [TestCase("https://user@www.apache.org/licenses/LICENSE-2.0")]
        [TestCase("https://www.apache.org:80/licenses/LICENSE-2.0")]
        [TestCase("http://www.apache.org:443/licenses/LICENSE-2.0")]
        [TestCase("https://www.apache.org/licenses/LICENSE-2.0?x=1")]
        [TestCase("https://www.apache.org/licenses/LICENSE-2.0?")]
        [TestCase("https://www.apache.org/licenses/LICENSE-2.0#terms")]
        [TestCase("ftp://www.apache.org/licenses/LICENSE-2.0")]
        [TestCase("/licenses/LICENSE-2.0")]
        [TestCase("https://www.apache.org/licenses/%4cICENSE-2.0")]
        [TestCase("https://www.apache.org/x/../licenses/LICENSE-2.0")]
        [TestCase("https://www.apache.org/licenses/./LICENSE-2.0")]
        [TestCase("https://www.apache.org\\licenses\\LICENSE-2.0")]
        [TestCase("https://www.apache.org/licenses")]
        [TestCase("https://scripts.sil.org/cms/scripts/page.php?item_id=OFL_web")]
        [TestCase("https://spdx.org/licenses/mit.html")]
        [TestCase("https://spdx.org/licenses/ISC.html")]
        [TestCase("Not licensed under https://www.apache.org/licenses/LICENSE-2.0")]
        [TestCase("For comparison see https://www.apache.org/licenses/LICENSE-2.0")]
        [TestCase("https://www.apache.org/licenses/LICEN\nSE-2.0")]
        [TestCase("https://www.apache.org./licenses/LICENSE-2.0")]
        public void UrlSpellingAndContextCannotBypassAllowlist(string input) {
            Assert.That(Analyze(input).Hints, Is.Empty);
        }
        static FontLicenseAnalysis Analyze(string input) {
            var face = ReadFace(Name.Text(LicenseDescriptionNameId, input));
            return FontLicenseAnalyzer.Analyze(face);
        }
        static void AssertSingle(string input, string id, FontLicenseIdentifierKind kind, string rule, ushort nameId = 13) {
            var face = ReadFace(Name.Text(nameId, input));
            var result = FontLicenseAnalyzer.Analyze(face);
            Assert.That(result.Hints, Has.Count.EqualTo(1), input);
            var hint = result.Hints[0];
            using(Assert.EnterMultipleScope()) {
                Assert.That((hint.Identifier, hint.IdentifierKind, hint.RuleId), Is.EqualTo((id, kind, rule)), input);
                Assert.That(hint.SourceRecord, Is.SameAs(face.NameRecords[0]));
                Assert.That(hint.SourceRecord.Value, Is.EqualTo(input));
                Assert.That(result.IsSourceComplete, Is.True);
            }
        }
        internal static FontMetadata.Face ReadFace(params Name[] names) {
            byte[] table = Names(names);
            byte[] data = Sfnt(("name", table));
            var metadata = FontMetadataReader.Read(data);
            return metadata.Faces[0];
        }
    }
}
