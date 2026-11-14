using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Tool.Tests {
    [TestFixture]
    public sealed class SpdxReportTests {
        [Test]
        public async Task FlagsAreIndependentAndLegacyShapeIsRetained() {
            byte[] font = Sfnt(("name", Names(Name.Text(13, "MIT"))));
            foreach(bool licenses in new[] { false, true })
                foreach(bool spdx in new[] { false, true })
                    foreach(bool records in new[] { false, true }) {
                        string[] flags = (licenses ? new[] { "--analyze-licenses" } : Array.Empty<string>())
                            .Concat(spdx ? new[] { "--analyze-spdx" } : Array.Empty<string>())
                            .Concat(records ? new[] { "--include-name-records" } : Array.Empty<string>()).ToArray();
                        var result = await ToolProcess.Run(new[] { "inspect", "-", "--format", "json" }.Concat(flags).ToArray(), font);
                        Assert.That(result.Code, Is.Zero);
                        var root = result.Json();
                        var face = root.GetProperty("results")[0].GetProperty("metadata").GetProperty("faces")[0];
                        Assert.That(root.TryGetProperty("spdxContext", out _), Is.EqualTo(spdx));
                        Assert.That(root.GetProperty("options").TryGetProperty("analyzeSpdx", out _), Is.EqualTo(spdx));
                        Assert.That(face.TryGetProperty("spdxAnalysis", out var analysis), Is.EqualTo(spdx));
                        Assert.That(face.TryGetProperty("licenseAnalysis", out _), Is.EqualTo(licenses));
                        Assert.That(face.TryGetProperty("nameRecords", out _), Is.EqualTo(records));
                        if(spdx)
                            Assert.That(analysis.GetProperty("declarations")[0].GetProperty("sourceRecord").GetProperty("value").GetString(), Is.EqualTo("MIT"));
                    }
        }
        [Test]
        public async Task ExplicitFailureUnknownAndReferenceKeepIndependentOutcomes() {
            byte[] font = Sfnt(("name", Names(Name.Text(13, "SPDX-License-Identifier: MIT OR"),
                Name.Text(13, "  SPDX-License-Identifier: MIT OR Unknown-1.0 OR LicenseRef-Custom  "))));
            var result = await ToolProcess.Run(["inspect", "-", "--format", "json", "--analyze-spdx"], font);
            Assert.That(result.Code, Is.Zero);
            var analysis = result.Json().GetProperty("results")[0].GetProperty("metadata").GetProperty("faces")[0].GetProperty("spdxAnalysis");
            Assert.That(analysis.GetProperty("isAnalysisComplete").GetBoolean(), Is.True);
            var declarations = analysis.GetProperty("declarations");
            Assert.That(declarations[0].GetProperty("parseStatus").GetString(), Is.EqualTo("invalidSyntax"));
            Assert.That(declarations[0].GetProperty("validation").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(declarations[0].GetProperty("parseDiagnostics")[0].GetProperty("span").GetProperty("start").GetInt32(), Is.EqualTo(6));
            Assert.That(declarations[1].GetProperty("sourceStart").GetInt32(), Is.EqualTo(27));
            var validation = declarations[1].GetProperty("validation");
            Assert.That(validation.GetProperty("hasIdentifierErrors").GetBoolean(), Is.True);
            Assert.That(validation.GetProperty("hasUnresolvedReferences").GetBoolean(), Is.True);
            Assert.That(validation.GetProperty("identifierOccurrences")[1].GetProperty("canonicalIdentifier").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(validation.GetProperty("references")[0].GetProperty("span").GetProperty("start").GetInt32(), Is.EqualTo(22));
        }
        [Test]
        public async Task ParserLimitsRetainExplicitAttemptsAndRecoverForLaterRecords() {
            string longIdentifier = new string('x', 257);
            var result = await ToolProcess.Run(["inspect", "-", "--format", "json", "--analyze-spdx"],
                Sfnt(("name", Names(Name.Text(13, longIdentifier), Name.Text(13, "SPDX-License-Identifier: " + longIdentifier), Name.Text(13, "MIT")))));
            Assert.That(result.Code, Is.Zero);
            var analysis = result.Json().GetProperty("results")[0].GetProperty("metadata").GetProperty("faces")[0].GetProperty("spdxAnalysis");
            Assert.That(analysis.GetProperty("isSourceComplete").GetBoolean(), Is.True);
            Assert.That(analysis.GetProperty("isAnalysisComplete").GetBoolean(), Is.False);
            Assert.That(analysis.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("FSI_PROBE_PARSE_LIMIT"));
            var declarations = analysis.GetProperty("declarations");
            Assert.That(declarations.GetArrayLength(), Is.EqualTo(2));
            Assert.That(declarations[0].GetProperty("parseStatus").GetString(), Is.EqualTo("limitExceeded"));
            Assert.That(declarations[0].GetProperty("expressionText").GetString(), Is.EqualTo(longIdentifier));
            Assert.That(declarations[0].GetProperty("validation").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(declarations[0].GetProperty("parseDiagnostics").GetArrayLength(), Is.EqualTo(1));
            Assert.That(declarations[1].GetProperty("parseStatus").GetString(), Is.EqualTo("success"));
        }
        [Test]
        public async Task DeclarationLimitDoesNotChangeReaderStatusOrExitCode() {
            var result = await ToolProcess.Run(["inspect", "-", "--format", "json", "--analyze-spdx"],
                Sfnt(("name", Names(Enumerable.Range(0, 257).Select(_ => Name.Text(13, "MIT")).ToArray()))));
            Assert.That(result.Code, Is.Zero);
            var source = result.Json().GetProperty("results")[0];
            Assert.That(source.GetProperty("status").GetString(), Is.EqualTo("complete"));
            var analysis = source.GetProperty("metadata").GetProperty("faces")[0].GetProperty("spdxAnalysis");
            Assert.That(analysis.GetProperty("declarations").GetArrayLength(), Is.EqualTo(256));
            Assert.That(analysis.GetProperty("isAnalysisComplete").GetBoolean(), Is.False);
            var diagnostic = analysis.GetProperty("diagnostics")[0];
            Assert.That(diagnostic.GetProperty("code").GetString(), Is.EqualTo("FSI_DECLARATION_LIMIT"));
            Assert.That(diagnostic.GetProperty("sourceRecord").GetProperty("index").GetInt32(), Is.EqualTo(256));
        }
        [Test]
        public async Task TruncatedValidationKeepsAllOccurrencesAndLateReferences() {
            string expression = "SPDX-License-Identifier: MIT OR " + string.Join(" OR ", Enumerable.Repeat("Unknown-1.0", 130)) + " OR LicenseRef-Custom";
            var result = await ToolProcess.Run(["inspect", "-", "--format", "json", "--analyze-spdx"], Sfnt(("name", Names(Name.Text(13, expression)))));
            Assert.That(result.Code, Is.Zero);
            var analysis = result.Json().GetProperty("results")[0].GetProperty("metadata").GetProperty("faces")[0].GetProperty("spdxAnalysis");
            Assert.That(analysis.GetProperty("isAnalysisComplete").GetBoolean(), Is.True);
            var validation = analysis.GetProperty("declarations")[0].GetProperty("validation");
            Assert.That(validation.GetProperty("diagnosticsTruncated").GetBoolean(), Is.True);
            Assert.That(validation.GetProperty("diagnostics").GetArrayLength(), Is.EqualTo(128));
            Assert.That(validation.GetProperty("identifierOccurrences").GetArrayLength(), Is.EqualTo(131));
            Assert.That(validation.GetProperty("hasIdentifierErrors").GetBoolean(), Is.True);
            Assert.That(validation.GetProperty("hasUnresolvedReferences").GetBoolean(), Is.True);
            Assert.That(validation.GetProperty("references")[0].GetProperty("licenseId").GetString(), Is.EqualTo("Custom"));
        }
        [Test]
        public async Task UnreadableSourcesHaveContextWithoutInventedFaces() {
            var unknown = await ToolProcess.Run(["inspect", "-", "--format", "json", "--analyze-spdx"], [1, 2, 3]);
            Assert.That(unknown.Code, Is.EqualTo(1));
            Assert.That(unknown.Json().TryGetProperty("spdxContext", out _), Is.True);
            Assert.That(unknown.Json().GetProperty("results")[0].GetProperty("metadata").ValueKind, Is.EqualTo(JsonValueKind.Null));
            var missing = await ToolProcess.Run(["inspect", Guid.NewGuid().ToString("N") + ".ttf", "--format", "json", "--analyze-spdx"]);
            Assert.That(missing.Code, Is.EqualTo(3));
            Assert.That(missing.Json().GetProperty("results")[0].GetProperty("metadata").ValueKind, Is.EqualTo(JsonValueKind.Null));
            byte[] collection = Collection([("name", Names(Name.Text(1, "Example")))], [("name", Names(Name.Text(13, "MIT")))]);
            U32(collection, 12, uint.MaxValue);
            var partial = await ToolProcess.Run(["inspect", "-", "--format", "json", "--analyze-spdx"], collection);
            var faces = partial.Json().GetProperty("results")[0].GetProperty("metadata").GetProperty("faces");
            Assert.That(partial.Code, Is.EqualTo(1));
            Assert.That(faces[0].GetProperty("spdxAnalysis").GetProperty("isSourceComplete").GetBoolean(), Is.False);
            Assert.That(faces[1].GetProperty("spdxAnalysis").GetProperty("declarations").GetArrayLength(), Is.EqualTo(1));
        }
        [Test]
        public async Task PlainOutputEscapesSourceAndLabelsOutcomes() {
            var result = await ToolProcess.Run(["inspect", "-", "--analyze-spdx"],
                Sfnt(("name", Names(Name.Text(13, "SPDX-License-Identifier: MIT\u001b[31m")))));
            Assert.That(result.Code, Is.Zero);
            Assert.That(result.Output, Does.Contain("parse invalidSyntax").And.Contain("Source complete: true").And.Contain("Analysis complete: true"));
            Assert.That(result.Output.Contains((char)27), Is.False);
        }
        [Test]
        public void DuplicateFlagIsRejected() {
            Assert.That(CommandOptions.TryParse(["inspect", "-", "--analyze-spdx", "--analyze-spdx"], out _), Is.False);
        }
        [Test]
        public async Task CancellationDuringSpdxOutputRetainsExit130() {
            using(var cancellation = new CancellationTokenSource())
            using(var input = new MemoryStream(Sfnt(("name", Names(Enumerable.Range(0, 1000).Select(_ => Name.Text(13, "MIT OR Apache-2.0")).ToArray())))))
            using(var output = new CancelOutput(cancellation)) {
                var error = new StringWriter();
                int code = await Command.RunAsync(["inspect", "-", "--format", "json", "--analyze-spdx"], input, output, error, cancellation.Token);
                Assert.That(code, Is.EqualTo(130));
                Assert.That(error.ToString(), Does.Contain("CLI_CANCELLED"));
            }
        }
        sealed class CancelOutput(CancellationTokenSource cancellation) : MemoryStream {
            public override void Write(ReadOnlySpan<byte> buffer) {
                base.Write(buffer);
                if(Length > 16384)
                    cancellation.Cancel();
            }
        }
    }
}
