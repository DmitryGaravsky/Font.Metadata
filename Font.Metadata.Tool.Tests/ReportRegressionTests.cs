using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;
using static Fonts.Metadata.Tests.WebFontFixtures;

namespace Fonts.Metadata.Tool.Tests {
    [TestFixture]
    public sealed class ReportRegressionTests {
        static async Task<ProcessResult> Inspect(byte[] bytes, params string[] options) {
            return await ToolProcess.Run(new[] { "inspect", "-", "--format", "json" }.Concat(options).ToArray(), bytes);
        }
        static JsonElement Metadata(ProcessResult result) {
            return result.Json().GetProperty("results")[0].GetProperty("metadata");
        }
        [Test]
        public async Task SkippedRecordsKeepIndexesAndDuplicateEvidence() {
            byte[] name = Names(new Name(13, [0]), Name.Text(13, "MIT"), Name.Text(13, "MIT", language: 1049), Name.Text(65000, " raw\0 "));
            var result = await Inspect(Sfnt(("name", name)), "--include-name-records", "--analyze-licenses");
            Assert.That(result.Code, Is.EqualTo(1));
            var face = Metadata(result).GetProperty("faces")[0];
            var records = face.GetProperty("nameRecords").EnumerateArray().ToArray();
            Assert.That(records.Select(r => r.GetProperty("index").GetInt32()), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(records[2].GetProperty("value").GetString(), Is.EqualTo(" raw\0 "));
            var analysis = face.GetProperty("licenseAnalysis");
            Assert.That(analysis.GetProperty("isSourceComplete").GetBoolean(), Is.False);
            Assert.That(analysis.GetProperty("hints").EnumerateArray().Select(h => h.GetProperty("sourceRecord").GetProperty("index").GetInt32()), Is.EqualTo(new[] { 1, 2 }));
        }
        [Test]
        public async Task CollectionPlaceholderDoesNotRenumberLaterFace() {
            byte[] data = Collection([("name", NameData("First"))], [("name", NameData("Second"))]);
            U32(data, 12, uint.MaxValue);
            var result = await Inspect(data, "--analyze-licenses");
            Assert.That(result.Code, Is.EqualTo(1));
            var faces = Metadata(result).GetProperty("faces");
            Assert.That(faces.GetArrayLength(), Is.EqualTo(2));
            Assert.That(faces[0].GetProperty("index").GetInt32(), Is.Zero);
            Assert.That(faces[0].GetProperty("nameTableStatus").GetString(), Is.EqualTo("notRead"));
            Assert.That(faces[1].GetProperty("index").GetInt32(), Is.EqualTo(1));
            Assert.That(faces[1].GetProperty("familyName").GetString(), Is.EqualTo("Second"));
        }
        [TestCase(false)]
        [TestCase(true)]
        public async Task FailedAndUnsupportedContainersRetainTheirMetadata(bool unsupported) {
            byte[] data = unsupported ? Collection([("name", NameData())]) : [0, 1, 0, 0];
            if(unsupported)
                U32(data, 4, 0x30000);
            var result = await Inspect(data);
            Assert.That(result.Code, Is.EqualTo(1));
            Assert.That(result.Json().GetProperty("results")[0].GetProperty("status").GetString(), Is.EqualTo(unsupported ? "unsupported" : "failed"));
            Assert.That(Metadata(result).ValueKind, Is.EqualTo(JsonValueKind.Object));
        }
        [Test]
        public async Task CompressedWorkLimitIsNotAnIoError() {
            byte[] data = Woff2([new Table("name", new byte[2 * 1024 * 1024])]);
            var result = await Inspect(data);
            Assert.That(result.Code, Is.EqualTo(1));
            var diagnostics = result.Json().GetProperty("results")[0].GetProperty("diagnostics").EnumerateArray();
            Assert.That(diagnostics.Any(d => d.GetProperty("category").GetString() == "limitExceeded"), Is.True);
            Assert.That(result.Error, Is.Empty);
        }
        [Test]
        public async Task RawUInt32IsNotSignedOrConvertedToAString() {
            var result = await Inspect(Eot(Example(), eudc: Example(), eudcFlags: 0x80000000));
            Assert.That(result.Code, Is.EqualTo(1));
            Assert.That(Metadata(result).GetProperty("eot").GetProperty("eudcFlags").GetUInt32(), Is.EqualTo(0x80000000u));
        }
        [Test]
        public async Task PlainCompleteSnapshot() {
            var result = await ToolProcess.Run(["inspect", "-"], Example());
            const string expected = """
Source: stdin null
Status: complete
Format: sfnt
EOT header status: missing
Face 0:
  Source: container
  SFNT flavor: 0x00010000
  Name table: complete
  OS/2 table: complete
  Vendor ID: "TEST"
  Embedding permissions (raw): 0x0000
  Family name: "Example"
Diagnostics: none
""";
            Assert.That(result.Output.Replace("\r\n", "\n").TrimEnd('\n'), Is.EqualTo(expected.Replace("\r\n", "\n")));
            Assert.That(result.Code, Is.Zero);
        }
        [TestCase(false)]
        [TestCase(true)]
        public async Task LargeEvidenceReportFlushesIncrementallyAndObservesCancellation(bool cancel) {
            byte[] name = Names(Enumerable.Range(0, 3000).Select(_ => Name.Text(13, "MIT")).ToArray());
            using(var cancellation = new CancellationTokenSource())
            using(var input = new MemoryStream(Sfnt(("name", name))))
            using(var output = new CountingOutput(cancel ? cancellation : null))
            using(var error = new StringWriter()) {
                int code = await Command.RunAsync(["inspect", "-", "--format", "json", "--include-name-records", "--analyze-licenses"], input, output, error, cancellation.Token);
                Assert.That(code, Is.EqualTo(cancel ? 130 : 0));
                Assert.That(output.MaximumWrite, Is.LessThan(100_000));
                Assert.That(output.Total, Is.GreaterThan(cancel ? 100_000 : 1_000_000));
                if(cancel)
                    Assert.That(error.ToString(), Does.Contain("CLI_CANCELLED"));
            }
        }
        [Test]
        public async Task DiagnosticSuppressionIsPreserved() {
            var records = new[] { Name.Text(13, "MIT") }.Concat(Enumerable.Range(0, 300).Select(_ => new Name(1, [0]))).ToArray();
            var result = await Inspect(Sfnt(("name", Names(records))), "--analyze-licenses");
            Assert.That(result.Code, Is.EqualTo(1));
            var diagnostics = result.Json().GetProperty("results")[0].GetProperty("diagnostics").EnumerateArray().ToArray();
            Assert.That(diagnostics.Length, Is.EqualTo(256));
            Assert.That(diagnostics[^1].GetProperty("code").GetString(), Is.EqualTo("FM_DIAGNOSTICS_TRUNCATED"));
            Assert.That(Metadata(result).GetProperty("faces")[0].GetProperty("licenseAnalysis").GetProperty("hints").GetArrayLength(), Is.EqualTo(1));
        }
        [Test]
        public async Task InputBudgetsResetBetweenFilesAndOptionsCanSeparatePaths() {
            string path = Path.GetTempFileName();
            try {
                byte[] data = Example();
                File.WriteAllBytes(path, data);
                var result = await ToolProcess.Run(["inspect", path, "--max-input-bytes", data.Length.ToString(), "--format", "json", path]);
                Assert.That(result.Code, Is.Zero);
                Assert.That(result.Json().GetProperty("results").GetArrayLength(), Is.EqualTo(2));
            }
            finally {
                File.Delete(path);
            }
        }
        sealed class CountingOutput : Stream {
            readonly CancellationTokenSource? cancellation;
            internal CountingOutput(CancellationTokenSource? cancellation) {
                this.cancellation = cancellation;
            }
            internal long Total { get; private set; }
            internal int MaximumWrite { get; private set; }
            public override bool CanRead { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override long Length { get { return Total; } }
            public override long Position { get { return Total; } set { throw new NotSupportedException(); } }
            public override void Write(byte[] buffer, int offset, int count) {
                Count(count);
            }
            public override void Write(ReadOnlySpan<byte> buffer) {
                Count(buffer.Length);
            }
            void Count(int count) {
                Total += count;
                MaximumWrite = Math.Max(MaximumWrite, count);
                if(Total > 100_000)
                    cancellation?.Cancel();
            }
            public override void Flush() {
            }
            public override int Read(byte[] buffer, int offset, int count) {
                throw new NotSupportedException();
            }
            public override long Seek(long offset, SeekOrigin origin) {
                throw new NotSupportedException();
            }
            public override void SetLength(long value) {
                throw new NotSupportedException();
            }
        }
    }
}
