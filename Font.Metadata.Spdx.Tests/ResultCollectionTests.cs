using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Spdx.Expressions;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Spdx.Tests {
    [TestFixture]
    public sealed class ResultCollectionTests {
        [Test]
        public void SectionValuesAndDiagnosticsSurviveAccessThroughSynchronizationRoots() {
            // U+1F600 maps to glyph 1. The wght axis has a reserved flag bit, which produces a warning.
            var cmap = Convert.FromHexString("000000010003000A0000000C000C00000000001C00000000000000010001F6000001F60000000001");
            var fvar = Convert.FromHexString("000100000010000200010014000000087767687400008000000100000001800000020100");
            var input = Sfnt(("cmap", cmap), ("fvar", fvar));
            var options = new FontReadOptions { IncludeUnicodeCoverage = true, IncludeVariationAxes = true };
            var result = FontMetadataReader.Inspect(input, options);
            var face = result.Faces.Single();
            AssertUnchangedThroughSyncRoot(result.Faces);
            AssertUnchangedThroughSyncRoot(face.UnicodeCoverage!.Ranges);
            AssertUnchangedThroughSyncRoot(face.VariationAxes!.Axes);
            AssertUnchangedThroughSyncRoot(face.VariationAxes.Diagnostics);
            Assert.That(face.UnicodeCoverage.Ranges.Single().Start, Is.EqualTo(0x1F600));
            Assert.That(face.VariationAxes.Axes.Single().Tag, Is.EqualTo("wght"));
            Assert.That(face.VariationAxes.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.NonConformantFvarTable));
        }
        [Test]
        public void DocumentEvidenceSurvivesAccessThroughItsSynchronizationRoot() {
            var result = FontLicenseDocumentAnalyzer.Analyze("MIT License", SpdxLicenseRegistry.Default);
            AssertUnchangedThroughSyncRoot(result.Evidence);
            Assert.That(result.Evidence.Single().Identifier, Is.EqualTo("MIT"));
            Assert.That(result.IsAnalysisComplete, Is.True);
        }
        static void AssertUnchangedThroughSyncRoot<T>(IReadOnlyList<T> values) {
            var expected = values.ToArray();
            Assert.That(expected, Is.Not.Empty);
            if(values is ICollection collection && collection.SyncRoot is IList list) {
                try {
                    list[0] = default(T);
                }
                catch(NotSupportedException) {
                    // Read-only list implementations may expose themselves as the lock object.
                }
            }
            Assert.That(values, Is.EqualTo(expected));
        }
    }
}
