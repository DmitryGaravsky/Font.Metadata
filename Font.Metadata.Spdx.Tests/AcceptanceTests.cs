using System;
using System.Linq;
using NUnit.Framework;
using Spdx.Expressions;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Spdx.Tests {
    [TestFixture]
    public sealed class AcceptanceTests {
        static FontMetadata.Face Face(params string[] values) {
            return FontMetadataReader.Read(Sfnt(("name", Names(values.Select(v => Name.Text(13, v)).ToArray())))).Faces[0];
        }
        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(2, true)]
        public void DiagnosticCapsAreInclusiveAndDoNotStopTraversal(int budget, bool oneOver) {
            int count = budget + (oneOver ? 1 : 0);
            var face = Face(Enumerable.Repeat("Apache-2.0", count).Append("MIT").ToArray());
            var result = FontSpdxAnalyzer.Analyze(face, SpdxLicenseRegistry.Default, new FontSpdxOptions {
                MaxDiagnostics = budget,
                ParseOptions = new SpdxParseOptions { MaxInputLength = 3 }
            });
            Assert.That(result.Diagnostics.Count, Is.EqualTo(budget));
            Assert.That(result.DiagnosticsTruncated, Is.EqualTo(oneOver));
            Assert.That(result.Diagnostics.Select(d => d.SourceRecord.Index), Is.EqualTo(Enumerable.Range(0, budget)));
            Assert.That(result.IsAnalysisComplete, Is.False);
            Assert.That(result.Declarations.Single().SourceRecord.Index, Is.EqualTo(count));
            Assert.That(result.Declarations.Single().ExpressionText, Is.EqualTo("MIT"));

            face = Face("SPDX-License-Identifier: " + string.Join(" OR ", Enumerable.Repeat("Unknown-1.0", count)) + " OR MIT OR LicenseRef-Late");
            result = FontSpdxAnalyzer.Analyze(face, SpdxLicenseRegistry.Default, new FontSpdxOptions {
                ValidationOptions = new SpdxValidationOptions { MaxDiagnostics = budget }
            });
            var validation = result.Declarations.Single().ValidationResult!;
            Assert.That(validation.Diagnostics.Count, Is.EqualTo(budget));
            Assert.That(validation.DiagnosticsTruncated, Is.EqualTo(oneOver));
            Assert.That(validation.IdentifierOccurrences.Count, Is.EqualTo(count + 1));
            Assert.That(validation.IdentifierOccurrences[^1].CanonicalIdentifier, Is.EqualTo("MIT"));
            Assert.That(validation.References.Single().LicenseId, Is.EqualTo("Late"));
            Assert.That(validation.HasIdentifierErrors, Is.True);
            Assert.That(validation.HasUnresolvedReferences, Is.True);
            Assert.That(result.IsAnalysisComplete, Is.True);
            Assert.That(result.DiagnosticsTruncated, Is.False);
        }
        [TestCase("  ")]
        [TestCase("SPDX-License-Identifier: ")]
        [TestCase("  MIT  ")]
        public void CharacterBudgetChargesBlankPrefixAndWhitespaceBeforeTrimming(string value) {
            var face = Face(value);
            foreach(int budget in new[] { value.Length, value.Length - 1 }) {
                var result = FontSpdxAnalyzer.Analyze(face, SpdxLicenseRegistry.Default,
                    new FontSpdxOptions { MaxTotalSourceCharacters = budget });
                Assert.That(result.IsAnalysisComplete, Is.EqualTo(budget == value.Length));
                if(budget < value.Length) {
                    Assert.That(result.Declarations, Is.Empty);
                    Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("FSI_SOURCE_CHARACTER_LIMIT"));
                    Assert.That(result.Diagnostics.Single().SourceRecord, Is.SameAs(face.NameRecords[0]));
                }
            }
        }
        [TestCase(FontSectionStatus.Missing)]
        [TestCase(FontSectionStatus.Complete)]
        [TestCase(FontSectionStatus.Partial)]
        [TestCase(FontSectionStatus.Invalid)]
        [TestCase(FontSectionStatus.Unsupported)]
        [TestCase(FontSectionStatus.NotRead)]
        public void SourceCompletenessUsesOnlyTheNameSection(FontSectionStatus status) {
            byte[] names = status == FontSectionStatus.Partial ? Names(new Name(13, [0]), Name.Text(13, "MIT")) : Names(Name.Text(13, "MIT"));
            if(status == FontSectionStatus.Invalid)
                names = [0, 0];
            if(status == FontSectionStatus.Unsupported)
                U16(names, 0, 2);
            byte[] bytes = status == FontSectionStatus.Missing ? Sfnt(("OS/2", Os2())) : Sfnt(("name", names));
            if(status == FontSectionStatus.NotRead) {
                bytes = Collection([("name", names)]);
                U32(bytes, 12, uint.MaxValue);
            }
            var face = FontMetadataReader.Inspect(bytes).Faces[0];
            Assert.That(face.NameTableStatus, Is.EqualTo(status));
            var result = FontSpdxAnalyzer.Analyze(face, SpdxLicenseRegistry.Default);
            Assert.That(result.IsSourceComplete, Is.EqualTo(status is FontSectionStatus.Complete or FontSectionStatus.Missing));
            Assert.That(result.IsAnalysisComplete, Is.True);
            Assert.That(result.Declarations.Count, Is.EqualTo(status is FontSectionStatus.Complete or FontSectionStatus.Partial ? 1 : 0));
        }
        [TestCase("SPDX-License-Identifier: MIT", "MIT", false, false)]
        [TestCase("SPDX-License-Identifier: Unknown-1.0", "Unknown-1.0", true, false)]
        [TestCase("SPDX-License-Identifier: Classpath-exception-2.0", "Classpath-exception-2.0", true, false)]
        [TestCase("SPDX-License-Identifier: LicenseRef-Custom", "LicenseRef-Custom", false, true)]
        [TestCase(" \r\n\tSPDX-License-Identifier: MIT OR Apache-2.0\r\n", "MIT OR Apache-2.0", false, false)]
        public void ExplicitOutcomesRetainTheExactSubstring(string source, string expression, bool errors, bool references) {
            var face = Face(source);
            var result = FontSpdxAnalyzer.Analyze(face, SpdxLicenseRegistry.Default);
            var declaration = result.Declarations.Single();
            Assert.That(declaration.RuleId, Is.EqualTo("FSI001"));
            Assert.That(declaration.SourceRecord, Is.SameAs(face.NameRecords[0]));
            Assert.That(declaration.ExpressionText, Is.EqualTo(expression));
            Assert.That(source.Substring(declaration.SourceStart, declaration.SourceLength), Is.EqualTo(expression));
            Assert.That(declaration.ParseResult.Status, Is.EqualTo(SpdxParseStatus.Success));
            Assert.That(declaration.ValidationResult!.HasIdentifierErrors, Is.EqualTo(errors));
            Assert.That(declaration.ValidationResult.HasUnresolvedReferences, Is.EqualTo(references));
            foreach(var occurrence in declaration.ValidationResult.IdentifierOccurrences)
                Assert.That(source.Substring(declaration.SourceStart + occurrence.Span.Start, occurrence.Span.Length), Is.EqualTo(occurrence.Identifier));
        }
        [Test]
        public void UrlHintsContradictionsAndDuplicateExpressionsStayIndependent() {
            var face = FontMetadataReader.Read(Sfnt(("name", Names(Name.Text(13, "MIT OR Apache-2.0"),
                Name.Text(14, "https://opensource.org/licenses/MIT"), Name.Text(13, "GPL-3.0-only"), Name.Text(13, "MIT OR Apache-2.0"))))).Faces[0];
            var hints = FontLicenseAnalyzer.Analyze(face);
            var result = FontSpdxAnalyzer.Analyze(face, SpdxLicenseRegistry.Default);
            Assert.That(result.Declarations.Select(d => d.SourceRecord.Index), Is.EqualTo(new[] { 0, 2, 3 }));
            Assert.That(result.Declarations.Select(d => d.ExpressionText), Is.EqualTo(new[] { "MIT OR Apache-2.0", "GPL-3.0-only", "MIT OR Apache-2.0" }));
            Assert.That(hints.Hints.Any(h => h.RuleId == "FLA004" && ReferenceEquals(h.SourceRecord, face.NameRecords[1])), Is.True);
            Assert.That(FontLicenseAnalyzer.Analyze(face).Hints.Select(h => (h.Identifier, h.RuleId, h.SourceRecord)),
                Is.EqualTo(hints.Hints.Select(h => (h.Identifier, h.RuleId, h.SourceRecord))));
        }
        [Test]
        public void Utf16SurrogateDiagnosticMapsBackToSource() {
            const string source = " \r\nSPDX-License-Identifier: MIT OR \U0001f600\r\n";
            var declaration = FontSpdxAnalyzer.Analyze(Face(source), SpdxLicenseRegistry.Default).Declarations.Single();
            Assert.That(declaration.SourceStart, Is.EqualTo(28));
            Assert.That(declaration.ExpressionText, Is.EqualTo("MIT OR \U0001f600"));
            Assert.That(declaration.SourceLength, Is.EqualTo(9));
            var span = declaration.ParseResult.Diagnostics.Single().Span;
            Assert.That(span.Start, Is.EqualTo(7));
            Assert.That(char.IsHighSurrogate(source[declaration.SourceStart + span.Start]), Is.True);
            Assert.That(declaration.ValidationResult, Is.Null);
        }
    }
}
