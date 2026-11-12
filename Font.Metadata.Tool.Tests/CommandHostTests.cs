using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Tool.Tests {
    [TestFixture]
    public sealed class CommandHostTests {
        [TestCase("plain")]
        [TestCase("json")]
        public async Task InputIoAndOutputIoHaveDifferentBoundaries(string format) {
            using(var input = new BrokenInput())
            using(var output = new MemoryStream())
            using(var error = new StringWriter()) {
                int code = await Command.RunAsync(["inspect", "-", "--format", format], input, output, error, default);
                Assert.That(code, Is.EqualTo(3));
                Assert.That(error.ToString(), Is.Empty);
                Assert.That(Encoding.UTF8.GetString(output.ToArray()), Does.Contain("CLI_IO_ERROR"));
            }
            using(var input = new MemoryStream(Example()))
            using(var output = new BrokenOutput())
            using(var error = new StringWriter()) {
                int code = await Command.RunAsync(["inspect", "-", "--format", format], input, output, error, default);
                Assert.That(code, Is.EqualTo(3));
                Assert.That(error.ToString(), Does.Contain("CLI_OUTPUT_ERROR"));
            }
        }
        [Test]
        public async Task FlushFailureAndClosedErrorChannelDoNotEscape() {
            using(var input = new MemoryStream(Example()))
            using(var output = new BrokenFlush())
            using(var error = new StreamWriter(new BrokenOutput())) {
                int code = await Command.RunAsync(["inspect", "-", "--format", "json"], input, output, error, default);
                Assert.That(code, Is.EqualTo(3));
            }
        }
        [Test]
        public async Task CancellationBeforeReadingHasNoFontResult() {
            using(var cancellation = new CancellationTokenSource())
            using(var input = new BrokenInput())
            using(var output = new MemoryStream())
            using(var error = new StringWriter()) {
                cancellation.Cancel();
                int code = await Command.RunAsync(["inspect", "-", "--format", "json"], input, output, error, cancellation.Token);
                Assert.That(code, Is.EqualTo(130));
                Assert.That(output.Length, Is.Zero);
                Assert.That(error.ToString(), Does.Contain("CLI_CANCELLED"));
            }
        }
        [Test]
        public async Task AwaitingRealAsynchronousPipeCanBeCancelled() {
            string pipeName = "font-cli-" + Guid.NewGuid().ToString("N");
            using(var server = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
            using(var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.Asynchronous))
            using(var cancellation = new CancellationTokenSource())
            using(var output = new MemoryStream())
            using(var error = new StringWriter()) {
                Task connected = server.WaitForConnectionAsync();
                await client.ConnectAsync();
                await connected;
                Task<int> run = Command.RunAsync(["inspect", "-", "--format", "json"], client, output, error, cancellation.Token);
                Assert.That(run.IsCompleted, Is.False);
                cancellation.Cancel();
                int code = await run.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.That(code, Is.EqualTo(130));
                Assert.That(error.ToString(), Does.Contain("CLI_CANCELLED"));
            }
        }
        [Test]
        public async Task CancellationAfterFirstResultStopsTheBatch() {
            string path = Path.GetTempFileName();
            try {
                File.WriteAllBytes(path, Example());
                using(var cancellation = new CancellationTokenSource())
                using(var output = new CancellingOutput(cancellation))
                using(var input = new MemoryStream())
                using(var error = new StringWriter()) {
                    int code = await Command.RunAsync(["inspect", path, "must-not-be-opened.ttf", "--format", "json"], input, output, error, cancellation.Token);
                    Assert.That(code, Is.EqualTo(130));
                    string text = Encoding.UTF8.GetString(output.ToArray());
                    Assert.That(text, Does.Not.Contain("must-not-be-opened"));
                }
            }
            finally {
                File.Delete(path);
            }
        }
        [Test]
        public async Task CultureDoesNotChangeJsonOrRawEvidence() {
            byte[] data = Sfnt(("name", Names(Name.Text(13, "MIT OR Apache-2.0"), Name.Text(65000, " Ω\0\r\n "))));
            string? baseline = null;
            CultureInfo original = CultureInfo.CurrentCulture;
            try {
                foreach(string culture in new[] { "en-US", "tr-TR", "ar-SA" }) {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    using(var input = new MemoryStream(data))
                    using(var output = new MemoryStream())
                    using(var error = new StringWriter()) {
                        int code = await Command.RunAsync(["inspect", "-", "--format", "json", "--include-name-records", "--analyze-licenses"], input, output, error, default);
                        Assert.That(code, Is.Zero);
                        byte[] bytes = output.ToArray();
                        Assert.That(bytes[0], Is.EqualTo((byte)'{'));
                        string text = Encoding.UTF8.GetString(bytes);
                        baseline ??= text;
                        Assert.That(text, Is.EqualTo(baseline));
                        using(var document = JsonDocument.Parse(text)) {
                            var face = document.RootElement.GetProperty("results")[0].GetProperty("metadata").GetProperty("faces")[0];
                            Assert.That(face.GetProperty("licenseAnalysis").GetProperty("hints").GetArrayLength(), Is.Zero);
                            Assert.That(face.GetProperty("nameRecords")[1].GetProperty("value").GetString(), Is.EqualTo(" Ω\0\r\n "));
                        }
                    }
                }
            }
            finally {
                CultureInfo.CurrentCulture = original;
            }
        }
        [Test]
        public void EveryEnumHasExplicitWireVocabulary() {
            Assert.That(Enum.GetValues<FontReadStatus>().Select(ReportValues.Status), Is.EqualTo(new[] { "notRecognized", "complete", "partial", "failed", "unsupported" }));
            Assert.That(Enum.GetValues<FontSectionStatus>().Select(ReportValues.Section), Is.EqualTo(new[] { "notRead", "missing", "complete", "partial", "invalid", "unsupported" }));
            Assert.That(Enum.GetValues<FontFormat>().Select(ReportValues.Format), Is.EqualTo(new[] { "unknown", "sfnt", "collection", "woff", "woff2", "eot" }));
            Assert.That(Enum.GetValues<FontFaceSource>().Select(ReportValues.Source), Is.EqualTo(new[] { "container", "eotMain", "eotEudc" }));
            Assert.That(Enum.GetValues<FontDiagnosticSeverity>().Select(ReportValues.Severity), Is.EqualTo(new[] { "warning", "error" }));
            Assert.That(Enum.GetValues<FontDiagnosticCategory>().Select(ReportValues.Category), Is.EqualTo(new[] { "malformedData", "limitExceeded", "unsupportedFeature", "unsupportedEncoding", "nonConformantData" }));
            Assert.That(Enum.GetValues<FontLicenseIdentifierKind>().Select(ReportValues.IdentifierKind), Is.EqualTo(new[] { "spdx", "family" }));
        }
        [Test]
        public void ArgumentValidationIncludesUnrepresentableProcessArguments() {
            Assert.That(CommandOptions.TryParse(["inspect", "bad\0path"], out _), Is.False);
            Assert.That(CommandOptions.TryParse(["inspect", "x", "--max-input-bytes", (Array.MaxLength - 1).ToString(CultureInfo.InvariantCulture)], out var options), Is.True);
            Assert.That(options.MaxInputBytes, Is.EqualTo(Array.MaxLength - 1));
            Assert.That(CommandOptions.TryParse(["inspect", "x", "--max-input-bytes", "0001"], out options), Is.True);
            Assert.That(options.MaxInputBytes, Is.EqualTo(1));
        }
        sealed class BrokenInput : MemoryStream {
            const string Failure = "Synthetic input failure.";
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
                throw new IOException(Failure);
            }
        }
        sealed class BrokenOutput : MemoryStream {
            const string Failure = "Synthetic output failure.";
            public override void Write(byte[] buffer, int offset, int count) {
                throw new IOException(Failure);
            }
            public override void Write(ReadOnlySpan<byte> buffer) {
                throw new IOException(Failure);
            }
        }
        sealed class BrokenFlush : MemoryStream {
            const string Failure = "Synthetic flush failure.";
            public override void Flush() {
                throw new IOException(Failure);
            }
        }
        sealed class CancellingOutput : MemoryStream {
            readonly CancellationTokenSource cancellation;
            internal CancellingOutput(CancellationTokenSource cancellation) {
                this.cancellation = cancellation;
            }
            public override void Flush() {
                base.Flush();
                if(Encoding.UTF8.GetString(ToArray()).Contains("\"complete\"", StringComparison.Ordinal))
                    cancellation.Cancel();
            }
        }
    }
}
