using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Spdx.Expressions;

namespace Fonts.Metadata.Spdx.Tests {
    [TestFixture]
    public sealed class LicenseDocumentTests {
        const string Tag = "SPDX-License-Identifier:";
        [Test]
        public void PreservesCompoundMalformedUnknownAndEmptyDeclarationsInOrder() {
            const string first = "\t SPDX-License-Identifier:\t(MIT OR Apache-2.0) AND GPL-2.0-only WITH Classpath-exception-2.0 \t";
            const string empty = "SPDX-License-Identifier: \t";
            const string second = "SPDX-License-Identifier: MIT AND";
            const string third = "SPDX-License-Identifier: Example-Unlisted OR DocumentRef-Example:LicenseRef-Custom";
            string text = $"\uFEFFNotes 🎵\r\n{first}\r\n{second}\r{third}\n{empty}\r\n{first}";
            var analysis = Analyze(text);
            Assert.That(analysis.IsAnalysisComplete, Is.True);
            Assert.That(analysis.RegistryIdentity, Is.SameAs(SpdxLicenseRegistry.Default.Identity));
            var entries = analysis.Evidence;
            Assert.That(entries.Select(item => item.Origin), Is.All.EqualTo(FontLicenseDocumentOrigin.SpdxDeclaration));
            Assert.That(entries.Select(item => item.SourceText), Is.EqualTo(new[] { first, second, third, empty, first }));
            Assert.That(entries.Select(item => item.ParseResult!.Status), Is.EqualTo(new[] {
                SpdxParseStatus.Success, SpdxParseStatus.InvalidSyntax, SpdxParseStatus.Success,
                SpdxParseStatus.InvalidSyntax, SpdxParseStatus.Success
            }));
            Assert.That(entries[0].SourceStart, Is.EqualTo(11));
            Assert.That(entries[4].SourceStart, Is.EqualTo(text.LastIndexOf(first, StringComparison.Ordinal)));
            Assert.That(entries[0].ParseResult!.Expression!.Root.Kind, Is.EqualTo(SpdxExpressionKind.And));
            Assert.That(entries[0].ValidationResult!.IdentifierOccurrences.Select(item => item.Identifier),
                Is.EqualTo(new[] { "MIT", "Apache-2.0", "GPL-2.0-only", "Classpath-exception-2.0" }));
            Assert.That(entries[1].ValidationResult, Is.Null);
            var validation = entries[2].ValidationResult!;
            Assert.That(validation.HasIdentifierErrors, Is.True);
            Assert.That(validation.HasUnresolvedReferences, Is.True);
            var unknown = validation.IdentifierOccurrences.Single();
            Assert.That(unknown.LookupStatus, Is.EqualTo(SpdxIdentifierLookupStatus.Unknown));
            Assert.That(text.Substring(entries[2].ExpressionStart!.Value + unknown.Span.Start, unknown.Span.Length), Is.EqualTo("Example-Unlisted"));
            Assert.That(entries[3].ExpressionStart, Is.EqualTo(entries[3].SourceStart + Tag.Length));
            Assert.That(entries[3].ExpressionText, Is.Empty);
            foreach(var item in entries) {
                Assert.That(item.SourceText, Is.EqualTo(text.Substring(item.SourceStart, item.SourceLength)));
                Assert.That(item.ExpressionText, Is.EqualTo(text.Substring(item.ExpressionStart!.Value, item.ExpressionLength!.Value)));
                Assert.That(item.Identifier, Is.Null);
            }
        }
        [TestCase("")]
        [TestCase("MIT")]
        [TestCase("NOTICE: some icons mention the MIT License.")]
        [TestCase("Reference SPDX-License-Identifier: MIT\n# SPDX-License-Identifier: MIT")]
        [TestCase("spdx-license-identifier: MIT\nSPDX-License-Identifier : MIT")]
        public void NoEvidenceFromIncidentalMentionsOrNearMisses(string content) {
            var result = Analyze(content);
            Assert.That(result.IsAnalysisComplete, Is.True);
            Assert.That(result.Evidence, Is.Empty);
        }
        [Test]
        public void LeadingBomIsFramingAndStillCountsInCoordinates() {
            const string text = "\uFEFF  SPDX-License-Identifier: MIT\r";
            var item = Analyze(text).Evidence.Single();
            Assert.That(item.SourceStart, Is.EqualTo(1));
            Assert.That(item.SourceText, Is.EqualTo("  SPDX-License-Identifier: MIT"));
            Assert.That(item.ExpressionStart, Is.EqualTo(28));
        }
        [TestCase("SIL-OFL-1.1.txt", "PREAMBLE", "OFL-1.1")]
        [TestCase("SPDX-MIT.txt", "Permission is hereby granted", "MIT")]
        [TestCase("0BSD.txt", "Permission to use", "0BSD")]
        [TestCase("Apache-2.0.txt", "TERMS AND CONDITIONS FOR USE", "Apache-2.0")]
        [TestCase("BSD-2-Clause.txt", "Redistribution and use", "BSD-2-Clause")]
        [TestCase("BSD-3-Clause.txt", "Redistribution and use", "BSD-3-Clause")]
        [TestCase("ISC.txt", "Permission to use", "ISC")]
        [TestCase("OFL-1.0.txt", "PREAMBLE", "OFL-1.0")]
        [TestCase("Ubuntu-font-1.0.txt", "PREAMBLE", "Ubuntu-font-1.0")]
        public void PublisherBodyIsReturnedVerbatim(string filename, string anchor, string identifier) {
            string document = ReadFixture(filename);
            int start = document.IndexOf(anchor, StringComparison.Ordinal);
            string text = "\uFEFF" + document.Replace("\n", "\r\n");
            var item = Analyze(text).Evidence.Single(item => item.Origin == FontLicenseDocumentOrigin.FullText);
            int expectedOffset = start + 1 + document[..start].Count(c => c == '\n');
            Assert.That(item.Identifier, Is.EqualTo(identifier));
            Assert.That(item.SourceStart, Is.EqualTo(expectedOffset));
            Assert.That(item.SourceText, Is.EqualTo(text[expectedOffset..].TrimEnd('\r', '\n')));
            Assert.That(item.SourceLength, Is.EqualTo(item.SourceText.Length));
            Assert.That(item.ParseResult, Is.Null);
            Assert.That(Analyze(text.TrimEnd('\r', '\n')[..^1] + "?").Evidence.Any(e => e.Origin == FontLicenseDocumentOrigin.FullText), Is.False);
            Assert.That(Analyze(text + "Additional conditions apply.").Evidence.Any(e => e.Origin == FontLicenseDocumentOrigin.FullText), Is.False);
        }
        [Test]
        public void ApacheAcceptsTheOfficialAppendixOrTheTermsAlone() {
            string text = ReadFixture("Apache-2.0.txt");
            int appendix = text.IndexOf("APPENDIX:", StringComparison.Ordinal);
            string termsOnly = text[..appendix].TrimEnd();
            var terms = Analyze(termsOnly).Evidence.Single(item => item.Origin == FontLicenseDocumentOrigin.FullText);
            Assert.That(terms.SourceText, Does.EndWith("END OF TERMS AND CONDITIONS"));
            var modifiedAppendix = Analyze(text[..appendix] + "APPENDIX: Different terms apply.");
            Assert.That(modifiedAppendix.Evidence.Select(item => item.Origin), Is.EqualTo(new[] { FontLicenseDocumentOrigin.Header }));
        }
        [Test]
        public void IscAcceptsTheEarlierPublishedVariant() {
            string text = ReadFixture("ISC.txt").Replace("and/or distribute", "and distribute");
            var body = Analyze(text).Evidence.Single(item => item.Origin == FontLicenseDocumentOrigin.FullText);
            Assert.That(body.Identifier, Is.EqualTo("ISC"));
            Assert.That(body.SourceText, Does.Contain("and distribute"));
        }
        [Test]
        public void OflAcceptsShortDatedSplitAndWrappedStandardHeaders() {
            string standard = ReadFixture("SIL-OFL-1.1.txt");
            string body = standard[standard.IndexOf("PREAMBLE", StringComparison.Ordinal)..];
            const string shortHeader = "SIL OPEN FONT LICENSE Version 1.1\n\nCopyright (c) 2042 Type Foundry\n\n";
            const string splitHeader = "Copyright 2042 Type Foundry\n" +
                "This Font Software is licensed under the SIL Open Font License,\r\nVersion 1.1.\n" +
                "This license is copied below, and is also available\nwith a FAQ at:\nhttp://scripts.sil.org/OFL\n\n" +
                "SIL OPEN FONT LICENSE\r\n\r\nVersion 1.1 - 26 February 2007\n\n";
            var shortResult = Analyze(shortHeader + body);
            Assert.That(shortResult.Evidence.Select(item => item.Origin), Is.EqualTo(new[] { FontLicenseDocumentOrigin.Header, FontLicenseDocumentOrigin.FullText }));
            Assert.That(shortResult.Evidence[1].SourceStart, Is.EqualTo(shortHeader.Length));
            var splitResult = Analyze(splitHeader + body.Replace(" -- ", " — "));
            Assert.That(splitResult.Evidence.Select(item => item.Identifier), Is.EqualTo(new[] { "OFL-1.1", "OFL-1.1", "OFL-1.1" }));
            Assert.That(splitResult.Evidence[0].SourceText, Does.Contain("License,\r\nVersion"));
            Assert.That(splitResult.Evidence[1].SourceText, Is.EqualTo("SIL OPEN FONT LICENSE\r\n\r\nVersion 1.1 - 26 February 2007"));
            Assert.That(splitResult.Evidence[2].Origin, Is.EqualTo(FontLicenseDocumentOrigin.FullText));
            Assert.That(splitResult.Evidence[2].SourceStart, Is.EqualTo(splitHeader.Length));
        }
        [Test]
        public void MitAcceptsCopyrightAndTypographyVariantsWithIndependentSpdxEvidence() {
            string fixture = ReadFixture("SPDX-MIT.txt");
            string body = fixture[fixture.IndexOf("Permission", StringComparison.Ordinal)..];
            const string prefix = "SPDX-License-Identifier: Apache-2.0\nThe MIT License (MIT)\nCopyright 2042 元 (🅰)\n" +
                "© 2043 Example\nAll rights reserved.\n\n";
            string variant = body.Replace("\"Software\"", "“Software”").Replace("\"AS IS\"", "“AS IS”")
                .Replace("of charge", "of\t \ncharge");
            var result = Analyze(prefix + variant);
            Assert.That(result.Evidence.Select(item => item.Origin), Is.EqualTo(new[] {
                FontLicenseDocumentOrigin.SpdxDeclaration, FontLicenseDocumentOrigin.Header, FontLicenseDocumentOrigin.FullText
            }));
            Assert.That(result.Evidence[0].ExpressionText, Is.EqualTo("Apache-2.0"));
            Assert.That(result.Evidence[1].SourceText, Is.EqualTo("The MIT License (MIT)"));
            Assert.That(result.Evidence[2].Identifier, Is.EqualTo("MIT"));
            Assert.That(result.Evidence[2].SourceStart, Is.EqualTo(prefix.Length));
            Assert.That(result.Evidence[2].SourceText, Is.EqualTo(variant.TrimEnd('\n')));
        }
        [TestCase("SIL-OFL-1.1.txt", "PREAMBLE", "may be sold by itself", "may be licensed by itself")]
        [TestCase("SPDX-MIT.txt", "Permission is hereby", "without restriction", "with restrictions")]
        public void DifferentTruncatedOrSupplementedBodyRetainsOnlyHeaders(string file, string anchor, string old, string replacement) {
            string fixture = ReadFixture(file);
            int bodyStart = fixture.IndexOf(anchor, StringComparison.Ordinal);
            foreach(string text in new[] {
                fixture.Replace(old, replacement),
                fixture[..bodyStart],
                fixture[..bodyStart] + fixture[bodyStart..].Replace(".", "", StringComparison.Ordinal),
                fixture + "\nAdditional conditions apply.",
                fixture + "\nSPDX-License-Identifier: MIT",
                "Additional conditions apply.\n" + fixture
            }) {
                var result = Analyze(text);
                Assert.That(result.IsAnalysisComplete, Is.True);
                Assert.That(result.Evidence.Any(item => item.Origin == FontLicenseDocumentOrigin.Header), Is.True);
                Assert.That(result.Evidence.Any(item => item.Origin == FontLicenseDocumentOrigin.FullText), Is.False);
            }
        }
        [Test]
        public void LimitsAreInclusiveAndPreserveAlreadyVisitedEvidence() {
            const string first = "SPDX-License-Identifier: MIT";
            const string second = "SPDX-License-Identifier: OFL-1.1";
            var atLimits = Analyze(first, new() { MaxSourceCharacters = first.Length, MaxEvidence = 1 });
            Assert.That(atLimits.IsAnalysisComplete, Is.True);
            Assert.That(atLimits.Evidence, Has.Count.EqualTo(1));
            var inputLimit = Analyze(first, new() { MaxSourceCharacters = first.Length - 1 });
            Assert.That(inputLimit.IsAnalysisComplete, Is.False);
            Assert.That(inputLimit.Evidence, Is.Empty);
            Assert.That(inputLimit.Diagnostic!.Code, Is.EqualTo("FDL_SOURCE_CHARACTER_LIMIT"));
            Assert.That(inputLimit.Diagnostic.SourceStart, Is.Null);
            var evidenceLimit = Analyze(first + "\n" + second, new() { MaxEvidence = 1 });
            Assert.That(evidenceLimit.IsAnalysisComplete, Is.False);
            Assert.That(evidenceLimit.Evidence.Single().SourceText, Is.EqualTo(first));
            Assert.That(evidenceLimit.Diagnostic!.Code, Is.EqualTo("FDL_EVIDENCE_LIMIT"));
            Assert.That(evidenceLimit.Diagnostic.SourceStart, Is.EqualTo(first.Length + 1));
            Assert.That(evidenceLimit.Diagnostic.SourceLength, Is.EqualTo(second.Length));
        }
        [TestCase("MIT License", "MIT", "SIL-OFL-1.1.txt", "PREAMBLE", "OFL-1.1")]
        [TestCase("SIL OPEN FONT LICENSE Version 1.1", "OFL-1.1", "SPDX-MIT.txt", "Permission is hereby", "MIT")]
        [TestCase("SIL OPEN\r\nFONT LICENSE", null, "SIL-OFL-1.1.txt", "PREAMBLE", "OFL-1.1")]
        public void HeadingAndBodyRetainIndependentIdentifiersAndSpans(string title, string? headingId,
            string filename, string anchor, string bodyId) {
            string fixture = ReadFixture(filename);
            string body = fixture[fixture.IndexOf(anchor, StringComparison.Ordinal)..].TrimEnd('\n');
            string prefix = "\uFEFF  " + title + "\t\r\n\r\n";
            string document = prefix + body + "\r\n";
            var analysis = Analyze(document);
            Assert.That(analysis.IsAnalysisComplete, Is.True);
            Assert.That(analysis.Evidence, Has.Count.EqualTo(2));
            var heading = analysis.Evidence[0];
            var match = analysis.Evidence[1];
            Assert.That((heading.Origin, heading.Identifier, heading.SourceStart, heading.SourceLength, heading.SourceText),
                Is.EqualTo((FontLicenseDocumentOrigin.Header, headingId, 3, title.Length, title)));
            Assert.That((match.Origin, match.Identifier, match.SourceStart, match.SourceLength, match.SourceText),
                Is.EqualTo((FontLicenseDocumentOrigin.FullText, bodyId, prefix.Length, body.Length, body)));
            var supplemented = Analyze(document + "Additional conditions apply.");
            Assert.That(supplemented.Evidence.Single().Origin, Is.EqualTo(FontLicenseDocumentOrigin.Header));
        }
        [Test]
        public void TruncatedValidationDiagnosticsPreserveCompletenessAndOccurrences() {
            string expression = string.Join(" OR ", Enumerable.Repeat("Unknown-Example", 140));
            var analysis = Analyze(Tag + " " + expression);
            var validation = analysis.Evidence.Single().ValidationResult!;
            Assert.That(analysis.IsAnalysisComplete, Is.True);
            Assert.That(analysis.Diagnostic, Is.Null);
            Assert.That(validation.DiagnosticsTruncated, Is.True);
            Assert.That(validation.Diagnostics, Has.Count.EqualTo(128));
            Assert.That(validation.IdentifierOccurrences, Has.Count.EqualTo(140));
        }
        [Test]
        public void ParserLimitRetainsAttemptAndSubsequentDeclarations() {
            string value = new string('x', 65_537);
            var result = Analyze($"{Tag} {value}\n{Tag} MIT");
            Assert.That(result.IsAnalysisComplete, Is.False);
            Assert.That(result.Diagnostic, Is.Null);
            Assert.That(result.Evidence[0].ExpressionText, Is.EqualTo(value));
            Assert.That(result.Evidence[0].ParseResult!.Status, Is.EqualTo(SpdxParseStatus.LimitExceeded));
            Assert.That(result.Evidence[1].ParseResult!.Status, Is.EqualTo(SpdxParseStatus.Success));
        }
        [Test]
        public void NullInvalidLimitsAndCancellationAreExplicit() {
            Assert.Throws<ArgumentNullException>(() => Analyze(null!));
            Assert.Throws<ArgumentNullException>(() => FontLicenseDocumentAnalyzer.Analyze("", null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => Analyze("", new() { MaxSourceCharacters = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Analyze("", new() { MaxEvidence = -1 }));
            Assert.Throws<OperationCanceledException>(() =>
                FontLicenseDocumentAnalyzer.Analyze(Tag, SpdxLicenseRegistry.Default, cancellationToken: new CancellationToken(true)));
        }
        static FontLicenseDocumentAnalysis Analyze(string text, FontLicenseDocumentOptions? options = null) {
            return FontLicenseDocumentAnalyzer.Analyze(text, SpdxLicenseRegistry.Default, options);
        }
        static string ReadFixture(string filename) {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", filename)).ReplaceLineEndings("\n");
        }
    }
}
