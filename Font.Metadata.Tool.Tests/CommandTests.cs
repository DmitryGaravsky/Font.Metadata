using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;
using static Fonts.Metadata.Tests.WebFontFixtures;

namespace Fonts.Metadata.Tool.Tests {
    [TestFixture]
    public sealed class CommandTests {
        string directory = null!;
        [SetUp]
        public void SetUp() {
            directory = Path.Combine(Path.GetTempPath(), "font-cli-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }
        [TearDown]
        public void TearDown() {
            Directory.Delete(directory, recursive: true);
        }
        string Save(byte[] data, string name = "font.bin") {
            string path = Path.Combine(directory, name);
            File.WriteAllBytes(path, data);
            return path;
        }
        static JsonElement First(ProcessResult result) {
            return result.Json().GetProperty("results")[0];
        }
        static JsonElement Face(ProcessResult result, int index = 0) {
            return First(result).GetProperty("metadata").GetProperty("faces")[index];
        }
        [TestCase("--help")]
        [TestCase("-h")]
        [TestCase("inspect", "--help")]
        [TestCase("inspect", "-h")]
        [TestCase]
        public async Task HelpDoesNotReadRedirectedInput(params string[] args) {
            var result = await ToolProcess.Run(args, keepInputOpen: true);
            Assert.That(result.Code, Is.Zero);
            Assert.That(result.Output, Does.Contain("Usage: font-metadata"));
            Assert.That(result.Output, Does.Contain("--analyze-licenses"));
            Assert.That(result.Error, Is.Empty);
        }
        [Test]
        public async Task VersionUsesStandardOutput() {
            var result = await ToolProcess.Run(["--version"]);
            Assert.That(result.Code, Is.Zero);
            Assert.That(result.Output.Trim(), Does.Match(@"^\d+\.\d+\.\d+"));
            Assert.That(result.Error, Is.Empty);
        }
        static IEnumerable<string[]> BadArguments() {
            yield return ["inspect"];
            yield return ["inspect", "--"];
            yield return ["file.ttf"];
            yield return ["inspect", "x", "--format", "text"];
            yield return ["inspect", "x", "--format"];
            yield return ["inspect", "x", "--format=json"];
            yield return ["inspect", "x", "--FORMAT", "json"];
            yield return ["inspect", "x", "--include-name-records", "--include-name-records"];
            yield return ["inspect", "x", "--help"];
            yield return ["inspect", "--version"];
            yield return ["inspect", ""];
            yield return ["inspect", " "];
            yield return ["inspect", "-", "-"];
            yield return ["inspect", "-", "x"];
            foreach(string limit in new[] { "0", "-1", "+1", "1M", "1.0", "999999999999", "2147483647" })
                yield return ["inspect", "missing.ttf", "--max-input-bytes", limit, "--format", "json"];
        }
        [TestCaseSource(nameof(BadArguments))]
        public async Task ArgumentsFailBeforeAnyOutputOrInput(string[] args) {
            var result = await ToolProcess.Run(args, keepInputOpen: true);
            Assert.That(result.Code, Is.EqualTo(2));
            Assert.That(result.Output, Is.Empty);
            Assert.That(result.Error, Does.Contain("CLI_INVALID_ARGUMENTS"));
        }
        [TestCase("sfnt")]
        [TestCase("otf")]
        [TestCase("collection")]
        [TestCase("woff")]
        [TestCase("woff2")]
        [TestCase("eot")]
        public async Task FormatsPreserveMetadataAndOptionalEvidence(string format) {
            byte[] name = NameData("Unicode Ω", "MIT");
            byte[] bytes = format switch {
                "sfnt" => Sfnt(("name", name), ("OS/2", Os2())),
                "otf" => Sfnt(0x4f54544f, ("name", name), ("OS/2", Os2())),
                "collection" => Collection([("name", name), ("OS/2", Os2())]),
                "woff" => Woff(new Table("name", name), new Table("OS/2", Os2())),
                "woff2" => Woff2([new Table("name", name), new Table("OS/2", Os2())]),
                _ => Eot(Sfnt(("name", name), ("OS/2", Os2())))
            };
            var result = await ToolProcess.Run(["inspect", Save(bytes), "--format", "json", "--include-name-records", "--analyze-licenses"]);
            Assert.That(result.Code, Is.Zero, result.Output);
            Assert.That(result.Error, Is.Empty);
            Assert.That(First(result).GetProperty("metadata").GetProperty("format").GetString(), Is.EqualTo(format == "otf" ? "sfnt" : format));
            var face = Face(result);
            Assert.That(face.GetProperty("familyName").GetString(), Is.EqualTo("Unicode Ω"));
            Assert.That(face.GetProperty("embeddingPermissions").GetInt32(), Is.Zero);
            Assert.That(face.GetProperty("fullName").ValueKind, Is.EqualTo(JsonValueKind.Null));
            var hint = face.GetProperty("licenseAnalysis").GetProperty("hints")[0];
            Assert.That(hint.GetProperty("identifier").GetString(), Is.EqualTo("MIT"));
            Assert.That(hint.GetProperty("sourceRecord").GetProperty("value").GetString(), Is.EqualTo("MIT"));
            Assert.That(face.GetProperty("nameRecords").GetArrayLength(), Is.EqualTo(2));
        }
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task OptionalFieldsAndEvidenceAreIndependent(bool records, bool licenses) {
            string text = " \0MIT\r\n";
            byte[] data = Sfnt(("name", Names(Name.Text(13, text), Name.Text(14, "https://scripts.sil.org/OFL"))));
            var args = new List<string> { "inspect", Save(data), "--format", "json" };
            if(records)
                args.Add("--include-name-records");
            if(licenses)
                args.Add("--analyze-licenses");
            var result = await ToolProcess.Run(args.ToArray());
            Assert.That(result.Code, Is.Zero);
            var face = Face(result);
            Assert.That(face.TryGetProperty("nameRecords", out _), Is.EqualTo(records));
            Assert.That(face.TryGetProperty("licenseAnalysis", out var analysis), Is.EqualTo(licenses));
            if(licenses) {
                Assert.That(analysis.GetProperty("hints").GetArrayLength(), Is.EqualTo(2));
                Assert.That(analysis.GetProperty("hints")[0].GetProperty("sourceRecord").GetProperty("value").GetString(), Is.EqualTo(text));
                Assert.That(analysis.GetProperty("hints")[1].GetProperty("identifierKind").GetString(), Is.EqualTo("family"));
            }
        }
        [Test]
        public async Task MixedSourcesContinueAndIoTakesPriority() {
            string good = Save(Example(), "good.ttf");
            string bad = Save([1, 2, 3], "bad.ttf");
            string missing = Path.Combine(directory, "missing.ttf");
            var result = await ToolProcess.Run(["inspect", bad, missing, good, directory, good, "--format", "json"]);
            Assert.That(result.Code, Is.EqualTo(3));
            Assert.That(result.Error, Is.Empty);
            var results = result.Json().GetProperty("results").EnumerateArray().ToArray();
            Assert.That(results.Select(r => r.GetProperty("status").GetString()), Is.EqualTo(new[] { "notRecognized", "ioError", "complete", "ioError", "complete" }));
            Assert.That(results[0].GetProperty("metadata").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(results[1].GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("CLI_IO_ERROR"));
        }
        [TestCase("-")]
        [TestCase("--help")]
        [TestCase("-Unicode Ω font.bin")]
        public async Task LiteralPathsRetainTheirSource(string name) {
            Save(Example(), name);
            var result = await ToolProcess.Run(["inspect", "--format", "json", "--", name], directory: directory);
            Assert.That(result.Code, Is.Zero);
            Assert.That(First(result).GetProperty("source").GetProperty("kind").GetString(), Is.EqualTo("file"));
            Assert.That(First(result).GetProperty("source").GetProperty("path").GetString(), Is.EqualTo(name));
        }
        [Test]
        public async Task StdinIsBinaryAndOversizeDoesNotWaitForEof() {
            byte[] data = Example();
            var exact = await ToolProcess.Run(["inspect", "-", "--format", "json", "--max-input-bytes", data.Length.ToString()], data);
            Assert.That(exact.Code, Is.Zero);
            Assert.That(First(exact).GetProperty("source").GetProperty("path").ValueKind, Is.EqualTo(JsonValueKind.Null));
            var over = await ToolProcess.Run(["inspect", "-", "--format", "json", "--max-input-bytes", (data.Length - 1).ToString()], data, keepInputOpen: true);
            Assert.That(over.Code, Is.EqualTo(1));
            Assert.That(First(over).GetProperty("status").GetString(), Is.EqualTo("failed"));
            Assert.That(First(over).GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("FM_LIMIT_EXCEEDED"));
        }
        [TestCase(false)]
        [TestCase(true)]
        public async Task NonfontOrPrefixedFontIsNotSearched(bool prefix) {
            byte[] data = prefix ? new byte[4].Concat(Example()).ToArray() : [];
            var result = await ToolProcess.Run(["inspect", "-", "--format", "json"], data);
            Assert.That(result.Code, Is.EqualTo(1));
            Assert.That(First(result).GetProperty("status").GetString(), Is.EqualTo("notRecognized"));
        }
        [Test]
        public async Task EotPreservesHeaderAndEudcAfterMainFailure() {
            byte[] data = Eot([0, 1, 0, 0], eudc: Example());
            var result = await ToolProcess.Run(["inspect", "-", "--format", "json", "--analyze-licenses"], data);
            Assert.That(result.Code, Is.EqualTo(1));
            var metadata = First(result).GetProperty("metadata");
            Assert.That(metadata.GetProperty("eot").GetProperty("familyName").GetString(), Is.EqualTo("Container Family"));
            Assert.That(metadata.GetProperty("eotHeaderStatus").GetString(), Is.EqualTo("complete"));
            var faces = metadata.GetProperty("faces");
            Assert.That(faces[1].GetProperty("index").GetInt32(), Is.EqualTo(1));
            Assert.That(faces[1].GetProperty("source").GetString(), Is.EqualTo("eotEudc"));
            Assert.That(faces[1].GetProperty("familyName").GetString(), Is.EqualTo("Example"));
            Assert.That(faces[0].GetProperty("familyName").ValueKind, Is.EqualTo(JsonValueKind.Null));
        }
        [Test]
        public async Task BrokenNameDoesNotDiscardOs2OrClaimLicenseCompleteness() {
            byte[] data = Sfnt(("name", new byte[3]), ("OS/2", Os2()));
            var result = await ToolProcess.Run(["inspect", "-", "--format", "json", "--analyze-licenses"], data);
            Assert.That(result.Code, Is.EqualTo(1));
            var face = Face(result);
            Assert.That(face.GetProperty("vendorId").GetString(), Is.EqualTo("TEST"));
            Assert.That(face.GetProperty("nameTableStatus").GetString(), Is.EqualTo("invalid"));
            Assert.That(face.GetProperty("licenseAnalysis").GetProperty("isSourceComplete").GetBoolean(), Is.False);
        }
        [Test]
        public async Task CompressedFailurePreservesDiagnosticCoordinates() {
            byte[] data = Woff(new Table("name", NameData(new string('A', 200)), true), new Table("OS/2", Os2()));
            int offset = ReadOffset(data, 68), size = ReadOffset(data, 72);
            data[offset + size - 1] ^= 1;
            var result = await ToolProcess.Run(["inspect", "-", "--format", "json"], data);
            Assert.That(result.Code, Is.EqualTo(1));
            var diagnostics = First(result).GetProperty("diagnostics").EnumerateArray();
            Assert.That(diagnostics.Any(d => d.GetProperty("byteOffset").ValueKind == JsonValueKind.Number && d.GetProperty("byteOffset").GetInt64() == offset), Is.True);
            Assert.That(Face(result).GetProperty("vendorId").GetString(), Is.EqualTo("TEST"));
        }
        [Test]
        public async Task WarningsAndMissingNamesAreSuccessful() {
            byte[] data = Woff2([new Table("OS/2", Os2())]);
            data[14] = 1;
            var result = await ToolProcess.Run(["inspect", "-", "--format", "json", "--analyze-licenses"], data);
            Assert.That(result.Code, Is.Zero, result.Output);
            Assert.That(Face(result).GetProperty("nameTableStatus").GetString(), Is.EqualTo("missing"));
            Assert.That(Face(result).GetProperty("licenseAnalysis").GetProperty("isSourceComplete").GetBoolean(), Is.True);
            Assert.That(First(result).GetProperty("diagnostics")[0].GetProperty("severity").GetString(), Is.EqualTo("warning"));
        }
        [Test]
        public async Task PlainOutputEscapesUntrustedValuesAndKeepsRawFlags() {
            string value = "A\n\r\t\0\u001b]8;;evil\u009b\u202e\u2028\u2029";
            var result = await ToolProcess.Run(["inspect", "-", "--analyze-licenses"], Sfnt(("name", Names(Name.Text(1, value))), ("OS/2", Os2())));
            Assert.That(result.Code, Is.Zero);
            foreach(char control in new[] { '\0', '\u001b', '\u009b', '\u202e', '\u2028', '\u2029' })
                Assert.That(result.Output.Contains(control), Is.False);
            Assert.That(result.Output, Does.Contain("\\n"));
            Assert.That(result.Output, Does.Contain("Embedding permissions (raw): 0x0000"));
            Assert.That(result.Output, Does.Contain("No recognized license hints"));
            Assert.That(result.Output, Does.Contain("evidence, not a license decision"));
        }
    }
}
