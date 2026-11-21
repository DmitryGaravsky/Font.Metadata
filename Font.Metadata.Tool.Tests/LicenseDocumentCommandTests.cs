using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Fonts.Metadata.Tool.Tests {
    [TestFixture]
    public sealed class LicenseDocumentCommandTests {
        string directory = null!;
        [SetUp]
        public void SetUp() {
            directory = Directory.CreateTempSubdirectory("font-license-cli-").FullName;
        }
        [TearDown]
        public void TearDown() {
            Directory.Delete(directory, recursive: true);
        }
        [Test]
        public async Task BatchPreservesPathsHashesEvidenceAndIndependentErrors() {
            const string text = "\uFEFF🎵 NOTICE\r\n\tSPDX-License-Identifier: (MIT OR Apache-2.0) \t\r\n" +
                "SPDX-License-Identifier: Unlisted-Example\nSPDX-License-Identifier: MIT AND\n";
            byte[] source = Encoding.UTF8.GetBytes(text);
            byte[] invalid = [0xef, 0xbb, 0xbf, 0xc0, 0xaf];
            File.WriteAllBytes(Path.Combine(directory, "notes Ω"), source);
            File.WriteAllBytes(Path.Combine(directory, "bad.txt"), invalid);
            var result = await ToolProcess.Run(
                ["inspect-license", "missing.txt", "notes Ω", "--format", "json", "bad.txt", "notes Ω"], directory: directory);
            Assert.That(result.Code, Is.EqualTo(3));
            Assert.That(result.Error, Is.Empty);
            Assert.That(result.Output[0], Is.EqualTo('{'));
            Assert.That(result.Output, Does.EndWith("\n"));
            var report = result.Json();
            Assert.That(report.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
            Assert.That(report.GetProperty("spdxContext").GetProperty("recognitionProfile").GetString(), Is.EqualTo("license-document-v1"));
            var entries = report.GetProperty("results").EnumerateArray().ToArray();
            Assert.That(entries.Select(item => item.GetProperty("source").GetProperty("path").GetString()),
                Is.EqualTo(new[] { "missing.txt", "notes Ω", "bad.txt", "notes Ω" }));
            Assert.That(entries.Select(item => item.GetProperty("status").GetString()),
                Is.EqualTo(new[] { "ioError", "complete", "invalidEncoding", "complete" }));
            Assert.That(entries[0].GetProperty("sha256").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(entries[0].GetProperty("hasUtf8Bom").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(entries[1].GetProperty("sha256").GetString(), Is.EqualTo(Digest(source)));
            Assert.That(entries[2].GetProperty("sha256").GetString(), Is.EqualTo(Digest(invalid)));
            Assert.That(entries[1].GetProperty("hasUtf8Bom").GetBoolean(), Is.True);
            Assert.That(entries[2].GetProperty("hasUtf8Bom").GetBoolean(), Is.True);
            Assert.That(entries[2].GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("CLI_LICENSE_INVALID_UTF8"));
            Assert.That(entries[3].GetRawText(), Is.EqualTo(entries[1].GetRawText()));
            var items = entries[1].GetProperty("evidence").EnumerateArray().ToArray();
            Assert.That(items.Select(item => item.GetProperty("origin").GetString()), Is.All.EqualTo("spdxDeclaration"));
            Assert.That(items.Select(item => item.GetProperty("parseStatus").GetString()),
                Is.EqualTo(new[] { "success", "success", "invalidSyntax" }));
            Assert.That(items[0].GetProperty("sourceStart").GetInt32(), Is.EqualTo(12));
            foreach(var item in items) {
                AssertFragment(text, item, "source");
                AssertFragment(text, item, "expression");
            }
            var known = items[0].GetProperty("validation").GetProperty("identifierOccurrences");
            Assert.That(known.EnumerateArray().Select(item => item.GetProperty("canonicalIdentifier").GetString()),
                Is.EqualTo(new[] { "MIT", "Apache-2.0" }));
            var unknown = items[1].GetProperty("validation").GetProperty("identifierOccurrences")[0];
            Assert.That(unknown.GetProperty("lookupStatus").GetString(), Is.EqualTo("unknown"));
            Assert.That(items[2].GetProperty("validation").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(items[2].GetProperty("parseDiagnostics").GetArrayLength(), Is.EqualTo(1));
        }
        [Test]
        public async Task ByteLimitDistinguishesExactEofFromOversizedAndPreservesLaterInput() {
            const int limit = 1_048_576;
            byte[] exact = new byte[limit];
            Array.Fill(exact, (byte)' ');
            File.WriteAllBytes(Path.Combine(directory, "exact"), exact);
            File.WriteAllBytes(Path.Combine(directory, "large"), [.. exact, (byte)' ']);
            File.WriteAllText(Path.Combine(directory, "after"), "SPDX-License-Identifier: MIT\n");
            var result = await ToolProcess.Run(["inspect-license", "large", "exact", "after", "--format", "json"], directory: directory);
            Assert.That(result.Code, Is.EqualTo(1));
            Assert.That(result.Error, Is.Empty);
            var entries = result.Json().GetProperty("results");
            var tooLarge = entries[0];
            Assert.That(tooLarge.GetProperty("status").GetString(), Is.EqualTo("limitExceeded"));
            Assert.That(tooLarge.GetProperty("sha256").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(tooLarge.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("CLI_LICENSE_INPUT_LIMIT"));
            Assert.That(entries[1].GetProperty("status").GetString(), Is.EqualTo("complete"));
            Assert.That(entries[1].GetProperty("sha256").GetString(), Is.EqualTo(Digest(exact)));
            Assert.That(entries[1].GetProperty("hasUtf8Bom").GetBoolean(), Is.False);
            Assert.That(entries[2].GetProperty("evidence")[0].GetProperty("expressionText").GetString(), Is.EqualTo("MIT"));
        }
        [Test]
        public async Task UnknownAndMalformedDeclarationsAreCompleteEvidence() {
            File.WriteAllText(Path.Combine(directory, "NOTICE"), "SPDX-License-Identifier: Unknown-Test\nSPDX-License-Identifier: MIT OR\n");
            var result = await ToolProcess.Run(["inspect-license", "NOTICE", "--format", "json"], directory: directory);
            Assert.That(result.Code, Is.Zero);
            Assert.That(result.Json().GetProperty("results")[0].GetProperty("status").GetString(), Is.EqualTo("complete"));
        }
        [Test]
        public async Task FullBodyHasIndependentEvidenceWithoutSpdxFields() {
            string path = Path.Combine(AppContext.BaseDirectory, "TestData", "SPDX-MIT.txt");
            string text = File.ReadAllText(path);
            var result = await ToolProcess.Run(["inspect-license", path, "--format", "json"]);
            Assert.That(result.Code, Is.Zero);
            var evidence = result.Json().GetProperty("results")[0].GetProperty("evidence");
            Assert.That(evidence.EnumerateArray().Select(item => item.GetProperty("origin").GetString()),
                Is.EqualTo(new[] { "header", "fullText" }));
            var body = evidence[1];
            Assert.That(body.GetProperty("identifier").GetString(), Is.EqualTo("MIT"));
            Assert.That(body.GetProperty("sourceText").GetString(), Does.StartWith("Permission is hereby granted"));
            AssertFragment(text, body, "source");
            foreach(string name in new[] { "expressionStart", "expressionLength", "expressionText", "parseStatus", "validation" })
                Assert.That(body.GetProperty(name).ValueKind, Is.EqualTo(JsonValueKind.Null), name);
            Assert.That(body.GetProperty("parseDiagnostics").GetArrayLength(), Is.Zero);
        }
        [Test]
        public async Task AnalysisLimitKeepsDigestAndEarlierEvidence() {
            string declaration = "SPDX-License-Identifier: MIT\n";
            const int retained = 256;
            string text = string.Concat(Enumerable.Repeat(declaration, retained + 1));
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            File.WriteAllBytes(Path.Combine(directory, "NOTICE"), bytes);
            var result = await ToolProcess.Run(["inspect-license", "NOTICE", "--format", "json"], directory: directory);
            Assert.That(result.Code, Is.EqualTo(1));
            var item = result.Json().GetProperty("results")[0];
            Assert.That(item.GetProperty("status").GetString(), Is.EqualTo("limitExceeded"));
            Assert.That(item.GetProperty("sha256").GetString(), Is.EqualTo(Digest(bytes)));
            Assert.That(item.GetProperty("evidence").GetArrayLength(), Is.EqualTo(retained));
            var diagnostic = item.GetProperty("diagnostics")[0];
            Assert.That(diagnostic.GetProperty("code").GetString(), Is.EqualTo("FDL_EVIDENCE_LIMIT"));
            Assert.That(diagnostic.GetProperty("sourceStart").GetInt32(), Is.EqualTo(retained * declaration.Length));
        }
        [Test]
        public async Task NonUtf8BomFailsWithoutAutomaticEncodingDetection() {
            byte[] utf16 = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("SPDX-License-Identifier: MIT")];
            File.WriteAllBytes(Path.Combine(directory, "LICENSE"), utf16);
            var result = await ToolProcess.Run(["inspect-license", "LICENSE", "--format", "json"], directory: directory);
            Assert.That(result.Code, Is.EqualTo(1));
            var item = result.Json().GetProperty("results")[0];
            Assert.That(item.GetProperty("status").GetString(), Is.EqualTo("invalidEncoding"));
            Assert.That(item.GetProperty("sha256").GetString(), Is.EqualTo(Digest(utf16)));
            Assert.That(item.GetProperty("hasUtf8Bom").GetBoolean(), Is.False);
            Assert.That(item.GetProperty("evidence").GetArrayLength(), Is.Zero);
        }
        [Test]
        public async Task PlainIsDefaultAndOnlyNamedFilesSupplyEvidence() {
            File.WriteAllText(Path.Combine(directory, "NOTICE"), "This notice mentions MIT.");
            File.WriteAllText(Path.Combine(directory, "LICENSE"), "SPDX-License-Identifier: MIT");
            File.WriteAllText(Path.Combine(directory, "-"), "SIL OPEN FONT LICENSE Version 1.1");
            var result = await ToolProcess.Run(["inspect-license", "NOTICE", "--", "-"], directory: directory, keepInputOpen: true);
            Assert.That(result.Code, Is.Zero);
            Assert.That(result.Error, Is.Empty);
            Assert.That(result.Output, Does.Contain("No recognized evidence."));
            Assert.That(result.Output, Does.Contain("header").And.Contain("SIL OPEN FONT LICENSE Version 1.1"));
            Assert.That(result.Output, Does.Contain("SHA-256"));
            Assert.That(result.Output, Does.Not.Contain("spdxDeclaration").And.Not.Contain("fullText"));
        }
        [TestCase("inspect-license")]
        [TestCase("inspect-license", "-")]
        [TestCase("inspect-license", "LICENSE", "--analyze-spdx")]
        [TestCase("inspect-license", "LICENSE", "--format=json")]
        [TestCase("inspect-license", "LICENSE", "--format", "json", "--format", "plain")]
        public async Task InvalidArgumentsDoNotReadInputs(params string[] args) {
            var result = await ToolProcess.Run(args, directory: directory, keepInputOpen: true);
            Assert.That(result.Code, Is.EqualTo(2));
            Assert.That(result.Output, Is.Empty);
            Assert.That(result.Error, Does.Contain("CLI_INVALID_ARGUMENTS"));
        }
        [Test]
        public async Task HelpDescribesDocumentCommand() {
            var result = await ToolProcess.Run(["inspect-license", "--help"], keepInputOpen: true);
            Assert.That(result.Code, Is.Zero);
            Assert.That(result.Error, Is.Empty);
            Assert.That(result.Output, Does.Contain("font-metadata inspect-license").And.Contain("UTF-8"));
        }
        static string Digest(byte[] bytes) {
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        static void AssertFragment(string text, JsonElement evidence, string prefix) {
            int start = evidence.GetProperty(prefix + "Start").GetInt32();
            int length = evidence.GetProperty(prefix + "Length").GetInt32();
            Assert.That(evidence.GetProperty(prefix + "Text").GetString(), Is.EqualTo(text.Substring(start, length)));
        }
    }
}
