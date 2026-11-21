using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Spdx.Expressions;

namespace Fonts.Metadata.Spdx.Tests {
    [TestFixture]
    public sealed class LicenseDocumentCatalogTests {
        const string SampleBody = "Sample permission. Sample conditions.";
        const string SampleHeading = "Example Terms Version 1";
        const string SampleIdentifier = "LicenseRef-Example-Terms";
        [Test]
        public void CustomCatalogAppliesOnlyToTheSuppliedAnalysisAndLeavesSpdxIndependent() {
            string heading = "Example Terms\r\nVersion 1";
            string prefix = "\uFEFF" + heading + "\r\nNotice scope: project files\nSPDX-License-Identifier: MIT\n";
            const string originalBody = "Sample permission.\n\nSample\tconditions.";
            string text = prefix + originalBody;
            var options = new FontLicenseDocumentOptions { Catalog = FontLicenseDocumentCatalog.Parse(SampleCatalog()) };
            var analysis = FontLicenseDocumentAnalyzer.Analyze(text, SpdxLicenseRegistry.Default, options);
            Assert.That(analysis.IsAnalysisComplete, Is.True);
            Assert.That(analysis.Evidence.Select(e => e.Origin), Is.EqualTo(new[] {
                FontLicenseDocumentOrigin.Header, FontLicenseDocumentOrigin.SpdxDeclaration, FontLicenseDocumentOrigin.FullText
            }));
            var title = analysis.Evidence[0];
            Assert.That((title.Identifier, title.SourceStart, title.SourceText), Is.EqualTo((SampleIdentifier, 1, heading)));
            Assert.That(analysis.Evidence[1].ValidationResult!.HasIdentifierErrors, Is.False);
            var body = analysis.Evidence[2];
            Assert.That((body.Identifier, body.SourceStart, body.SourceLength, body.SourceText),
                Is.EqualTo((SampleIdentifier, prefix.Length, originalBody.Length, originalBody)));
            var subsequent = FontLicenseDocumentAnalyzer.Analyze(text, SpdxLicenseRegistry.Default);
            Assert.That(subsequent.Evidence.Single().Origin, Is.EqualTo(FontLicenseDocumentOrigin.SpdxDeclaration));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void CanReplaceOrExtendBuiltInProfiles(bool includeBuiltIn) {
            var catalog = FontLicenseDocumentCatalog.Parse(SampleCatalog(), includeBuiltIn);
            var options = new FontLicenseDocumentOptions { Catalog = catalog };
            string mit = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "SPDX-MIT.txt"));
            var mitResult = FontLicenseDocumentAnalyzer.Analyze(mit, SpdxLicenseRegistry.Default, options);
            Assert.That(mitResult.Evidence.Any(e => e.Origin == FontLicenseDocumentOrigin.FullText), Is.EqualTo(includeBuiltIn));
            var custom = FontLicenseDocumentAnalyzer.Analyze(SampleHeading + "\n" + SampleBody, SpdxLicenseRegistry.Default, options);
            Assert.That(custom.Evidence.Single(e => e.Origin == FontLicenseDocumentOrigin.FullText).Identifier, Is.EqualTo(SampleIdentifier));
        }
        [Test]
        public void EmptyCatalogStillParsesExplicitDeclarations() {
            var options = new FontLicenseDocumentOptions { Catalog = FontLicenseDocumentCatalog.Parse("[]") };
            var analysis = FontLicenseDocumentAnalyzer.Analyze("MIT License\nSPDX-License-Identifier: MIT", SpdxLicenseRegistry.Default, options);
            Assert.That(analysis.Evidence.Single().ParseResult!.Status, Is.EqualTo(SpdxParseStatus.Success));
        }
        [Test]
        public void ACustomWrappedPhraseCannotConsumeAnExplicitDeclarationLine() {
            const string declaration = "SPDX-License-Identifier: MIT";
            string profile = Profile(SampleIdentifier, SampleBody, "Notice " + declaration, "Notice scope: ");
            var options = new FontLicenseDocumentOptions { Catalog = FontLicenseDocumentCatalog.Parse("[" + profile + "]") };
            string text = "Notice\r\n\t" + declaration + "\n" + SampleBody;
            var analysis = FontLicenseDocumentAnalyzer.Analyze(text, SpdxLicenseRegistry.Default, options);
            var item = analysis.Evidence.Single();
            Assert.That(item.Origin, Is.EqualTo(FontLicenseDocumentOrigin.SpdxDeclaration));
            Assert.That(item.SourceStart, Is.EqualTo(8));
            Assert.That(item.ExpressionText, Is.EqualTo("MIT"));
        }
        [Test]
        public void IncompatibleFrontMatterDoesNotAllowARecognizedBody() {
            string other = Profile("LicenseRef-Other", "Other permission.", "Other Terms", "Other scope: ");
            string catalogJson = "[" + Profile(SampleIdentifier, SampleBody, SampleHeading, "Notice scope: ") + "," + other + "]";
            var options = new FontLicenseDocumentOptions { Catalog = FontLicenseDocumentCatalog.Parse(catalogJson) };
            string text = SampleHeading + "\nNotice scope: fonts\nOther scope: assets\n" + SampleBody;
            var analysis = FontLicenseDocumentAnalyzer.Analyze(text, SpdxLicenseRegistry.Default, options);
            Assert.That(analysis.Evidence.Single().Origin, Is.EqualTo(FontLicenseDocumentOrigin.Header));
        }
        [TestCase("{}")]
        [TestCase("[null]")]
        [TestCase("[{\"identifier\":\"Valid\",\"bodySha256\":[\"abc\"]}]")]
        [TestCase("[{\"identifier\":\"missingDigest\"}]")]
        [TestCase("[{\"identifier\":\"First\",\"identifier\":\"Second\"}]")]
        public void InvalidCatalogJsonIsRejected(string json) {
            Assert.Throws<JsonException>(() => FontLicenseDocumentCatalog.Parse(json));
        }
        [Test]
        public void AmbiguousUnsupportedAndOversizedCatalogsAreRejected() {
            string profile = Profile(SampleIdentifier, SampleBody, SampleHeading, "Notice scope: ");
            string duplicateIdentifier = Profile(SampleIdentifier.ToLowerInvariant(), "Other permission.", "Other Title", "Scope: ");
            string duplicateDigest = Profile("LicenseRef-Other", SampleBody, "Other Title", "Scope: ");
            string duplicateHeading = Profile("LicenseRef-Other", "Other permission.", "Example Terms Version 1", "Scope: ");
            foreach(string conflicting in new[] { duplicateIdentifier, duplicateDigest, duplicateHeading })
                Assert.Throws<JsonException>(() => FontLicenseDocumentCatalog.Parse($"[{profile},{conflicting}]"));
            Assert.Throws<JsonException>(() => FontLicenseDocumentCatalog.Parse(SampleCatalog().Replace("wrappedPhrase", "regex")));
            Assert.Throws<JsonException>(() => FontLicenseDocumentCatalog.Parse(
                "[" + Profile("mit", SampleBody, SampleHeading, "Notice scope: ") + "]", includeBuiltIn: true));
            Assert.Throws<JsonException>(() => FontLicenseDocumentCatalog.Parse(new string(' ', 262_145)));
            Assert.Throws<JsonException>(() => FontLicenseDocumentCatalog.Parse("[" + string.Join(',', Enumerable.Repeat(profile, 65)) + "]"));
        }
        [TestCase("wholeLine", "Terms", "wholeLine", "TERMS", false)]
        [TestCase("wholeLine", "Terms", "wholeLine", "Terms", true)]
        [TestCase("wholeLine", "Terms", "wholeLineIgnoreCase", "TERMS", true)]
        [TestCase("wholeLineIgnoreCase", "Terms", "wholeLineIgnoreCase", "TERMS", true)]
        [TestCase("wholeLine", "Terms  V1", "wholeLineIgnoreCase", "Terms V1", false)]
        [TestCase("wholeLine", "Terms\tV1", "wrappedPhrase", "TERMS V1", true)]
        [TestCase("wholeLineIgnoreCase", "Terms V1", "wrappedPhrase", "TERMS V1", true)]
        public void HeadingConflictsFollowMatchSemanticsInEitherProfileOrder(string firstKind, string firstText,
            string secondKind, string secondText, bool ambiguous) {
            string first = Profile("First", "First body.", firstText, "Scope: ", firstKind);
            string second = Profile("Second", "Second body.", secondText, "Scope: ", secondKind);
            foreach(string json in new[] { $"[{first},{second}]", $"[{second},{first}]" }) {
                if(ambiguous) {
                    Assert.Throws<JsonException>(() => FontLicenseDocumentCatalog.Parse(json));
                    continue;
                }
                var options = new FontLicenseDocumentOptions { Catalog = FontLicenseDocumentCatalog.Parse(json) };
                var analysis = FontLicenseDocumentAnalyzer.Analyze(firstText + "\n" + secondText, SpdxLicenseRegistry.Default, options);
                Assert.That(analysis.Evidence.Select(item => item.Identifier), Is.EqualTo(new[] { "First", "Second" }));
            }
        }
        static string SampleCatalog() {
            return "[" + Profile(SampleIdentifier, SampleBody, SampleHeading, "Notice scope: ") + "]";
        }
        static string Profile(string identifier, string body, string heading, string notice, string headingKind = "wrappedPhrase") {
            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
            return JsonSerializer.Serialize(new {
                identifier,
                headings = new[] { new { text = heading, match = headingKind } },
                frontMatter = new[] { new { text = notice, match = "prefixWithValue" } },
                bodySha256 = new[] { digest }
            });
        }
    }
}
