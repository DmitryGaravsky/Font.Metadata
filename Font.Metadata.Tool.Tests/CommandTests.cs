using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Fonts.Metadata.Tool.Tests {
    [TestFixture]
    public sealed class CommandTests {
        const int ProcessTimeoutMilliseconds = 30_000;
        const int InspectionUnavailableExitCode = 1, InvalidArgumentsExitCode = 2;
        const string ProcessTimeoutMessage = "The CLI process did not exit within 30 seconds.";

        [TestCase("--help")]
        [TestCase("-h")]
        [TestCase("")]
        public void HelpReportsTheCurrentCapabilities(string argument) {
            var result = Run(argument);
            using(Assert.EnterMultipleScope()) {
                Assert.That(result.ExitCode, Is.Zero);
                Assert.That(result.Output, Does.Contain("Usage: font-metadata"));
                Assert.That(result.Output, Does.Contain("not implemented"));
                Assert.That(result.Error, Is.Empty);
            }
        }
        [Test]
        public void VersionUsesStandardOutput() {
            var result = Run("--version");
            using(Assert.EnterMultipleScope()) {
                Assert.That(result.ExitCode, Is.Zero);
                Assert.That(result.Output.Trim(), Does.Match(@"^\d+\.\d+\.\d+"));
                Assert.That(result.Error, Is.Empty);
            }
        }
        [Test]
        public void InspectionCannotReportSuccessBeforeCliInspectionExists() {
            var result = Run("inspect", "does-not-exist.ttf", "--format", "json");
            using(Assert.EnterMultipleScope()) {
                Assert.That(result.ExitCode, Is.EqualTo(InspectionUnavailableExitCode));
                Assert.That(result.Output, Is.Empty);
                Assert.That(result.Error, Does.Contain("not implemented"));
            }
        }
        [Test]
        public void UnknownArgumentsUseStandardError() {
            var result = Run("--unknown");
            using(Assert.EnterMultipleScope()) {
                Assert.That(result.ExitCode, Is.EqualTo(InvalidArgumentsExitCode));
                Assert.That(result.Output, Is.Empty);
                Assert.That(result.Error, Does.Contain("Unknown arguments"));
            }
        }
        static (int ExitCode, string Output, string Error) Run(params string[] arguments) {
            var start = new ProcessStartInfo("dotnet") {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "tool", "Font.Metadata.Tool.dll"));
            foreach(string argument in arguments) {
                if(argument.Length > 0)
                    start.ArgumentList.Add(argument);
            }
            using(var process = new Process { StartInfo = start }) {
                process.Start();
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(ProcessTimeoutMilliseconds)) {
                    process.Kill(entireProcessTree: true);
                    throw new TimeoutException(ProcessTimeoutMessage);
                }
                return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
            }
        }
    }
}
