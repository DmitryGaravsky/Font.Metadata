using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Tool.Tests {
    [TestFixture]
    public sealed class CliFailureReviewTests {
        const string SyntheticFailure = "Synthetic output failure.";
        [TestCase("--help")]
        [TestCase("--version")]
        public async Task CancellationDuringInformationalOutputWinsOverSuccess(string argument) {
            using(var cancellation = new CancellationTokenSource())
            using(var output = new CallbackOutput(cancellation.Cancel))
            using(var input = new MemoryStream())
            using(var error = new StringWriter()) {
                int code = await Command.RunAsync([argument], input, output, error, cancellation.Token);
                Assert.That(code, Is.EqualTo(130));
                Assert.That(error.ToString(), Does.Contain("CLI_CANCELLED"));
            }
        }
        [TestCase("plain")]
        [TestCase("json")]
        public async Task FailedWriteIsNotRetriedByDisposal(string format) {
            using(var output = new FirstWriteFails())
            using(var input = new MemoryStream(Example()))
            using(var error = new StringWriter()) {
                int code = await Command.RunAsync(["inspect", "-", "--format", format], input, output, error, default);
                Assert.That(code, Is.EqualTo(3));
                Assert.That(output.Attempts, Is.EqualTo(1));
                Assert.That(output.Length, Is.Zero);
            }
        }
        [TestCase("plain")]
        [TestCase("json")]
        public async Task ClosingRealOutputPipeFailsTheProcess(string format) {
            var start = new ProcessStartInfo("dotnet") {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "tool", "Font.Metadata.Tool.dll"));
            foreach(string argument in new[] { "inspect", "-", "--format", format })
                start.ArgumentList.Add(argument);
            using(var process = new Process { StartInfo = start }) {
                process.Start();
                Task<string> error = process.StandardError.ReadToEndAsync();
                // Close the only read end before supplying the input; no timing race with plain output.
                process.StandardOutput.Close();
                try {
                    await process.StandardInput.BaseStream.WriteAsync(Example());
                    process.StandardInput.Close();
                }
                catch(IOException) {
                    // JSON may fail writing its prefix before it reads stdin.
                }
                try {
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
                finally {
                    if(!process.HasExited) {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync();
                    }
                }
                Assert.That(process.ExitCode, Is.EqualTo(3));
                Assert.That(await error, Does.Contain("CLI_OUTPUT_ERROR"));
            }
        }
        [Test]
        public async Task LargeStdinProducerCanOutliveTheInputLimit() {
            var result = await ToolProcess.Run(["inspect", "-", "--max-input-bytes", "16", "--format", "json"], new byte[2 * 1024 * 1024]);
            Assert.That(result.Code, Is.EqualTo(1));
            Assert.That(result.Error, Is.Empty);
            var first = result.Json().GetProperty("results")[0];
            Assert.That(first.GetProperty("status").GetString(), Is.EqualTo("failed"));
            Assert.That(first.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("FM_LIMIT_EXCEEDED"));
        }
        [TestCase(false)]
        [TestCase(true)]
        public async Task ShortReadsRespectTheProbeLimitAndCancellation(bool cancel) {
            using(var cancellation = new CancellationTokenSource())
            using(var input = new ShortInput(cancel ? cancellation : null))
            using(var output = new MemoryStream())
            using(var error = new StringWriter()) {
                int code = await Command.RunAsync(["inspect", "-", "--max-input-bytes", "16", "--format", "json"], input, output, error, cancellation.Token);
                Assert.That(code, Is.EqualTo(cancel ? 130 : 1));
                Assert.That(input.Position, Is.EqualTo(cancel ? 3 : 17));
            }
        }
        [Test]
        public async Task ArgumentFailurePrecedesCancellationAndInputIo() {
            using(var cancellation = new CancellationTokenSource())
            using(var input = new ThrowingInput(cancellation))
            using(var output = new MemoryStream())
            using(var error = new StringWriter()) {
                cancellation.Cancel();
                int code = await Command.RunAsync(["inspect", "-", "extra.ttf"], input, output, error, cancellation.Token);
                Assert.That(code, Is.EqualTo(2));
                Assert.That(output.Length, Is.Zero);
                Assert.That(error.ToString(), Does.Contain("CLI_INVALID_ARGUMENTS"));
            }
        }
        [Test]
        public async Task CancellationPrecedesAnInputIoFailure() {
            using(var cancellation = new CancellationTokenSource())
            using(var input = new ThrowingInput(cancellation))
            using(var output = new MemoryStream())
            using(var error = new StringWriter()) {
                int code = await Command.RunAsync(["inspect", "-", "--format", "json"], input, output, error, cancellation.Token);
                Assert.That(code, Is.EqualTo(130));
                Assert.That(error.ToString(), Does.Contain("CLI_CANCELLED"));
                Assert.That(Encoding.UTF8.GetString(output.ToArray()), Does.Not.Contain("ioError"));
            }
        }
        sealed class ShortInput : MemoryStream {
            readonly CancellationTokenSource? cancellation;
            internal ShortInput(CancellationTokenSource? cancellation) : base(new byte[100]) {
                this.cancellation = cancellation;
            }
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) {
                int count = Read(buffer.Span.Slice(0, Math.Min(3, buffer.Length)));
                cancellation?.Cancel();
                return ValueTask.FromResult(count);
            }
        }
        sealed class ThrowingInput : MemoryStream {
            readonly CancellationTokenSource cancellation;
            internal ThrowingInput(CancellationTokenSource cancellation) {
                this.cancellation = cancellation;
            }
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) {
                cancellation.Cancel();
                throw new IOException(SyntheticFailure);
            }
        }
        sealed class CallbackOutput : MemoryStream {
            readonly Action callback;
            internal CallbackOutput(Action callback) {
                this.callback = callback;
            }
            public override void Flush() {
                callback();
                base.Flush();
            }
        }
        sealed class FirstWriteFails : MemoryStream {
            internal int Attempts { get; private set; }
            public override void Write(byte[] buffer, int offset, int count) {
                if(++Attempts == 1)
                    throw new IOException(SyntheticFailure);
                base.Write(buffer, offset, count);
            }
            public override void Write(ReadOnlySpan<byte> buffer) {
                if(++Attempts == 1)
                    throw new IOException(SyntheticFailure);
                // Avoid counting MemoryStream's forwarding overload as a second write.
                base.Write(buffer.ToArray(), 0, buffer.Length);
            }
        }
    }
}
