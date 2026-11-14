using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Spdx.Expressions;
using static Fonts.Metadata.Tests.FontFixtures;
using static Fonts.Metadata.Tests.WebFontFixtures;

namespace Fonts.Metadata.Spdx.Tests {
    [TestFixture]
    public sealed class AnalysisTests {
        static FontMetadata.Face Face(params string[] values) {
            return FontMetadataReader.Read(Sfnt(("name", Names(values.Select(v => Name.Text(13, v)).ToArray())))).Faces[0];
        }
        static FontSpdxAnalysis Analyze(FontMetadata.Face face, FontSpdxOptions? options = null) {
            return FontSpdxAnalyzer.Analyze(face, SpdxLicenseRegistry.Default, options);
        }
        [TestCase("MIT")]
        [TestCase("MIT OR Apache-2.0")]
        [TestCase("MIT AND (Apache-2.0 OR BSD-3-Clause)")]
        [TestCase("GPL-2.0-only WITH Classpath-exception-2.0")]
        [TestCase("mit OR Apache-2.0")]
        [TestCase("GPL-2.0+")]
        [TestCase("LicenseRef-Custom")]
        [TestCase("DocumentRef-Other:LicenseRef-Custom")]
        [TestCase("MIT OR Unknown-1.0")]
        public void BareAdmissionPreservesNativeResults(string value) {
            var face = Face(value);
            var result = Analyze(face);
            var declaration = result.Declarations.Single();
            Assert.That(result.IsAnalysisComplete, Is.True);
            Assert.That(declaration.RuleId, Is.EqualTo("FSI002"));
            Assert.That(declaration.SourceRecord, Is.SameAs(face.NameRecords[0]));
            Assert.That(declaration.ExpressionText, Is.EqualTo(value));
            Assert.That(declaration.ParseResult.Expression!.SourceText, Is.SameAs(declaration.ExpressionText));
            Assert.That(declaration.ValidationResult, Is.Not.Null);
        }
        [TestCase("Unknown-1.0")]
        [TestCase("Unknown-1.0 OR Unknown-2.0")]
        [TestCase("Classpath-exception-2.0")]
        [TestCase("MIT OR")]
        [TestCase("MIT or Apache-2.0")]
        [TestCase("Not licensed under MIT")]
        [TestCase("Licensed under MIT")]
        [TestCase("The MIT License")]
        [TestCase("\"MIT\"")]
        [TestCase("\u00a0MIT\u00a0")]
        [TestCase("MIT\0")]
        [TestCase("MIT.")]
        [TestCase("prose SPDX-License-Identifier: MIT")]
        [TestCase("spdx-license-identifier: MIT")]
        [TestCase(" \r\n\t")]
        public void RejectedBareTextDoesNotBecomeEvidence(string value) {
            var result = Analyze(Face(value));
            Assert.That(result.Declarations, Is.Empty);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.IsAnalysisComplete, Is.True);
        }
        [TestCase("SPDX-License-Identifier: MIT OR")]
        [TestCase("SPDX-License-Identifier:")]
        [TestCase("SPDX-License-Identifier: NONE")]
        [TestCase("SPDX-License-Identifier: NOASSERTION")]
        [TestCase("SPDX-License-Identifier: MIT\nlicense body")]
        [TestCase("SPDX-License-Identifier: MIT\nSPDX-License-Identifier: Apache-2.0")]
        [TestCase("SPDX-License-Identifier: MIT OR \U0001f600")]
        public void ExplicitFailureIsCompletedAttempt(string value) {
            var result = Analyze(Face(value));
            var declaration = result.Declarations.Single();
            Assert.That(result.IsAnalysisComplete, Is.True);
            Assert.That(declaration.RuleId, Is.EqualTo("FSI001"));
            Assert.That(declaration.ParseResult.Status, Is.EqualTo(SpdxParseStatus.InvalidSyntax));
            Assert.That(declaration.ValidationResult, Is.Null);
            foreach(var diagnostic in declaration.ParseResult.Diagnostics)
                Assert.That(diagnostic.Span.Start + diagnostic.Span.Length, Is.LessThanOrEqualTo(declaration.ExpressionText.Length));
        }
        [Test]
        public void CoordinatesAndEmptySuffixAreExact() {
            var declarations = Analyze(Face("  SPDX-License-Identifier: MIT OR Apache-2.0  ", "SPDX-License-Identifier: \r\n", "SPDX-License-Identifier: MIT OR")).Declarations;
            Assert.That(declarations[0].SourceStart, Is.EqualTo(27));
            Assert.That(declarations[0].SourceLength, Is.EqualTo(17));
            Assert.That(declarations[0].ValidationResult!.IdentifierOccurrences[1].Span.Start, Is.EqualTo(7));
            Assert.That(declarations[1].SourceStart, Is.EqualTo(24));
            Assert.That(declarations[1].SourceLength, Is.Zero);
            Assert.That(declarations[2].ParseResult.Diagnostics[0].Span.Start, Is.EqualTo(6));
            Assert.That(declarations[2].ParseResult.Diagnostics[0].Span.Length, Is.Zero);
        }
        [Test]
        public void ValidationFlagsSurviveTruncationAndKeepOccurrences() {
            var result = Analyze(Face("SPDX-License-Identifier: Unknown-1.0 OR Unknown-1.0 OR LicenseRef-X OR Classpath-exception-2.0"),
                new FontSpdxOptions { ValidationOptions = new SpdxValidationOptions { MaxDiagnostics = 1 } });
            var validation = result.Declarations.Single().ValidationResult!;
            Assert.That(validation.HasIdentifierErrors, Is.True);
            Assert.That(validation.HasUnresolvedReferences, Is.True);
            Assert.That(validation.DiagnosticsTruncated, Is.True);
            Assert.That(validation.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(validation.IdentifierOccurrences.Count, Is.EqualTo(3));
            Assert.That(validation.IdentifierOccurrences[^1].LookupStatus, Is.EqualTo(SpdxIdentifierLookupStatus.WrongKind));
            Assert.That(validation.IdentifierOccurrences.All(x => x.CanonicalIdentifier == null && x.IsDeprecated == null), Is.True);
            Assert.That(result.IsAnalysisComplete, Is.True);
        }
        [Test]
        public void SourceOrderAndCompletenessRemainIndependent() {
            byte[] names = Names(new Name(13, [0]), Name.Text(13, "MIT"), Name.Text(14, "MIT OR Apache-2.0"), Name.Text(13, "MIT", language: 1049));
            var face = FontMetadataReader.Inspect(Sfnt(("name", names))).Faces[0];
            var result = Analyze(face);
            Assert.That(result.IsSourceComplete, Is.False);
            Assert.That(result.IsAnalysisComplete, Is.True);
            Assert.That(result.Declarations.Select(d => d.SourceRecord.Index), Is.EqualTo(new[] { 1, 3 }));
            Assert.That(result.Declarations[1].SourceRecord, Is.SameAs(face.NameRecords[2]));
            Assert.That(((IList)result.Declarations).IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => ((IList)result.Diagnostics).Clear());
        }
        [Test]
        public void SharedCollectionsAndEudcKeepIdentity() {
            byte[] names = Names(Name.Text(13, "MIT OR Apache-2.0"));
            byte[] sfnt = Sfnt(("name", names));
            byte[] ttc = Collection([("name", names)], [("name", names)]);
            // Both directories point to the first face's immutable table bytes.
            int firstDirectory = ReadOffset(ttc, 12), secondDirectory = ReadOffset(ttc, 16);
            U32(ttc, secondDirectory + 20, (uint)ReadOffset(ttc, firstDirectory + 20));
            foreach(byte[] bytes in new[] { ttc, Woff2([new Table("name", names)], [[0], [0]]), Eot(sfnt, eudc: sfnt) }) {
                var faces = FontMetadataReader.Inspect(bytes).Faces;
                Assert.That(faces.Count, Is.EqualTo(2));
                for(int i = 0; i < faces.Count; i++) {
                    Assert.That(faces[i].Index, Is.EqualTo(i));
                    Assert.That(Analyze(faces[i]).Declarations.Single().SourceRecord, Is.SameAs(faces[i].NameRecords[0]));
                }
            }
        }
        [Test]
        public void ResultLimitOnlyStopsOnNextAdmittedAttempt() {
            var options = new FontSpdxOptions { MaxDeclarations = 1 };
            Assert.That(Analyze(Face("MIT", "", "prose"), options).IsAnalysisComplete, Is.True);
            var result = Analyze(Face("MIT", "", "Apache-2.0", "MIT"), options);
            Assert.That(result.Declarations.Count, Is.EqualTo(1));
            Assert.That(result.IsAnalysisComplete, Is.False);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("FSI_DECLARATION_LIMIT"));
            Assert.That(result.Diagnostics.Single().SourceRecord.Index, Is.EqualTo(2));
        }
        [Test]
        public void AggregateBudgetsChargeFullRepeatedTextAndOtherRecords() {
            var face = FontMetadataReader.Read(Sfnt(("name", Names(Name.Text(1, "Family"), Name.Text(13, " MIT "), Name.Text(13, " MIT "))))).Faces[0];
            Assert.That(Analyze(face, new FontSpdxOptions { MaxSourceRecords = 3, MaxTotalSourceCharacters = 10 }).IsAnalysisComplete, Is.True);
            var records = Analyze(face, new FontSpdxOptions { MaxSourceRecords = 2, MaxTotalSourceCharacters = 5 });
            Assert.That(records.Diagnostics.Single().Code, Is.EqualTo("FSI_SOURCE_RECORD_LIMIT"));
            var chars = Analyze(face, new FontSpdxOptions { MaxTotalSourceCharacters = 9 });
            Assert.That(chars.Diagnostics.Single().Code, Is.EqualTo("FSI_SOURCE_CHARACTER_LIMIT"));
            Assert.That(chars.Declarations.Count, Is.EqualTo(1));
            Assert.That(Analyze(face, new FontSpdxOptions { MaxTotalSourceCharacters = 4 }).Declarations, Is.Empty);
        }
        [Test]
        public void ParserLimitsRecoverAndMessagesTruncateWithoutHidingWork() {
            var options = new FontSpdxOptions { ParseOptions = new SpdxParseOptions { MaxInputLength = 3 }, MaxDiagnostics = 1 };
            var result = Analyze(Face("Apache-2.0", "Apache-2.0", "SPDX-License-Identifier: Apache-2.0", "MIT"), options);
            Assert.That(result.IsAnalysisComplete, Is.False);
            Assert.That(result.DiagnosticsTruncated, Is.True);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("FSI_PROBE_PARSE_LIMIT"));
            Assert.That(result.Declarations.Select(d => d.ParseResult.Status), Is.EqualTo(new[] { SpdxParseStatus.LimitExceeded, SpdxParseStatus.Success }));
            var combined = Analyze(Face("MIT", "Apache-2.0", "SPDX-License-Identifier: Apache-2.0"),
                new FontSpdxOptions { ParseOptions = options.ParseOptions, MaxDeclarations = 1 });
            Assert.That(combined.Diagnostics.Select(d => d.Code), Is.EqualTo(new[] { "FSI_PROBE_PARSE_LIMIT", "FSI_DECLARATION_LIMIT" }));
        }
        [TestCase("input", "MIT", 3)]
        [TestCase("identifier", "MIT", 3)]
        [TestCase("tokens", "MIT OR MIT", 3)]
        [TestCase("nodes", "MIT OR MIT", 3)]
        [TestCase("depth", "((MIT))", 2)]
        public void NativeBoundariesAreInclusive(string limit, string text, int required) {
            foreach(bool explicitAnnotation in new[] { false, true })
                foreach(int budget in new[] { required, required - 1 }) {
                    var parserOptions = limit switch {
                        "input" => new SpdxParseOptions { MaxInputLength = budget },
                        "identifier" => new SpdxParseOptions { MaxIdentifierLength = budget },
                        "tokens" => new SpdxParseOptions { MaxTokens = budget },
                        "nodes" => new SpdxParseOptions { MaxNodes = budget },
                        _ => new SpdxParseOptions { MaxParenthesisDepth = budget }
                    };
                    var result = Analyze(Face((explicitAnnotation ? "SPDX-License-Identifier: " : "") + text),
                        new FontSpdxOptions { ParseOptions = parserOptions });
                    bool fits = budget == required;
                    Assert.That(result.IsAnalysisComplete, Is.EqualTo(fits));
                    if(fits || explicitAnnotation) {
                        var declaration = result.Declarations.Single();
                        Assert.That(declaration.ExpressionText, Is.EqualTo(text));
                        Assert.That(declaration.ParseResult.Status, Is.EqualTo(fits ? SpdxParseStatus.Success : SpdxParseStatus.LimitExceeded));
                        Assert.That(declaration.ValidationResult != null, Is.EqualTo(fits));
                        Assert.That(result.Diagnostics, Is.Empty);
                    }
                    else {
                        Assert.That(result.Declarations, Is.Empty);
                        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("FSI_PROBE_PARSE_LIMIT"));
                    }
                }
        }
        [Test]
        public void NativeTreeRetainsEveryNodeKindGroupingOrderAndSpelling() {
            const string text = "GPL-2.0+ AND (mit OR DocumentRef-Other:LicenseRef-Custom WITH Classpath-exception-2.0)";
            var declaration = Analyze(Face(text)).Declarations.Single();
            var root = (SpdxAndExpression)declaration.ParseResult.Expression!.Root;
            Assert.That(root.Span.Start, Is.Zero);
            Assert.That(root.Span.Length, Is.EqualTo(text.Length));
            Assert.That(root.Operands.Count, Is.EqualTo(2));
            var license = (SpdxLicenseExpression)root.Operands[0];
            Assert.That(license.Identifier, Is.EqualTo("GPL-2.0"));
            Assert.That(license.OrLater, Is.True);
            var group = (SpdxGroupExpression)root.Operands[1];
            var alternative = (SpdxOrExpression)group.Inner;
            Assert.That(alternative.Operands.Count, Is.EqualTo(2));
            Assert.That(((SpdxLicenseExpression)alternative.Operands[0]).Identifier, Is.EqualTo("mit"));
            var with = (SpdxWithExpression)alternative.Operands[1];
            Assert.That(with.ExceptionIdentifier, Is.EqualTo("Classpath-exception-2.0"));
            var reference = (SpdxReferenceExpression)with.Operand;
            Assert.That(reference.DocumentId, Is.EqualTo("Other"));
            Assert.That(reference.LicenseId, Is.EqualTo("Custom"));
            var validation = declaration.ValidationResult!;
            Assert.That(validation.IdentifierOccurrences[0].IsDeprecated, Is.True);
            Assert.That(validation.IdentifierOccurrences[1].CanonicalIdentifier, Is.EqualTo("MIT"));
            Assert.That(validation.HasIdentifierErrors, Is.False);
            Assert.That(validation.HasUnresolvedReferences, Is.True);
        }
        [Test]
        public void InvalidOptionsFailEvenForEmptyFaceAndNullOuterUsesDefaults() {
            var face = FontMetadataReader.Read(Sfnt(("OS/2", Os2()))).Faces[0];
            Assert.That(Analyze(face).IsSourceComplete, Is.True);
            var invalid = new[] {
                new FontSpdxOptions { MaxSourceRecords = 0 }, new FontSpdxOptions { MaxTotalSourceCharacters = -1 },
                new FontSpdxOptions { MaxDeclarations = 0 }, new FontSpdxOptions { MaxDiagnostics = 0 },
                new FontSpdxOptions { ParseOptions = null! }, new FontSpdxOptions { ValidationOptions = null! },
                new FontSpdxOptions { ParseOptions = new SpdxParseOptions { MaxParenthesisDepth = 257 } },
                new FontSpdxOptions { ParseOptions = new SpdxParseOptions { MaxInputLength = 0 } },
                new FontSpdxOptions { ParseOptions = new SpdxParseOptions { MaxIdentifierLength = 0 } },
                new FontSpdxOptions { ParseOptions = new SpdxParseOptions { MaxTokens = 0 } },
                new FontSpdxOptions { ParseOptions = new SpdxParseOptions { MaxNodes = 0 } },
                new FontSpdxOptions { ParseOptions = new SpdxParseOptions { MaxParenthesisDepth = 0 } },
                new FontSpdxOptions { ValidationOptions = new SpdxValidationOptions { MaxDiagnostics = 0 } }
            };
            foreach(var options in invalid)
                Assert.That(() => Analyze(face, options), Throws.InstanceOf<ArgumentException>());
            Assert.Throws<ArgumentNullException>(() => FontSpdxAnalyzer.Analyze(null!, SpdxLicenseRegistry.Default));
            Assert.Throws<ArgumentNullException>(() => FontSpdxAnalyzer.Analyze(face, null!));
            Assert.Throws<OperationCanceledException>(() => FontSpdxAnalyzer.Analyze(face, SpdxLicenseRegistry.Default, cancellationToken: new CancellationToken(true)));
        }
        [Test]
        public void ConcurrentCallsDoNotDependOnCultureOrMutateHints() {
            var face = Face("mit OR LicenseRef-Custom", "MIT");
            var before = FontLicenseAnalyzer.Analyze(face).Hints.Select(h => h.Identifier).ToArray();
            Parallel.For(0, 16, i => {
                var previous = CultureInfo.CurrentCulture;
                try {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(i % 2 == 0 ? "tr-TR" : "en-US");
                    Assert.That(Analyze(face).Declarations[0].ValidationResult!.IdentifierOccurrences[0].CanonicalIdentifier, Is.EqualTo("MIT"));
                }
                finally { CultureInfo.CurrentCulture = previous; }
            });
            Assert.That(FontLicenseAnalyzer.Analyze(face).Hints.Select(h => h.Identifier), Is.EqualTo(before));
        }
    }
}
