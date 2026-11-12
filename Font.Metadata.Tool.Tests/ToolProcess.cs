using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fonts.Metadata.Tool.Tests {
    internal sealed record ProcessResult(int Code, string Output, string Error) {
        internal JsonElement Json() {
            using(var document = JsonDocument.Parse(Output)) {
                return document.RootElement.Clone();
            }
        }
    }
    internal static class ToolProcess {
        const string TimeoutMessage = "The CLI did not exit within 30 seconds.";
        internal static async Task<ProcessResult> Run(string[] arguments, byte[]? input = null, string? directory = null, bool keepInputOpen = false) {
            var start = new ProcessStartInfo("dotnet") {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            if(directory != null)
                start.WorkingDirectory = directory;
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "tool", "Font.Metadata.Tool.dll"));
            foreach(string argument in arguments)
                start.ArgumentList.Add(argument);
            using(var process = new Process { StartInfo = start })
            using(var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30))) {
                process.Start();
                Task<string> output = process.StandardOutput.ReadToEndAsync(deadline.Token);
                Task<string> error = process.StandardError.ReadToEndAsync(deadline.Token);
                Task feed = FeedAsync(process.StandardInput.BaseStream, input, keepInputOpen, deadline.Token);
                try {
                    // Include input delivery in the deadline: an unread pipe must not hang the tests.
                    Task completion = Task.WhenAll(feed, process.WaitForExitAsync(), output, error);
                    await completion.WaitAsync(deadline.Token);
                }
                catch(OperationCanceledException)
                    when(deadline.IsCancellationRequested) {
                    if(!process.HasExited)
                        process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                    throw new TimeoutException(TimeoutMessage);
                }
                return new ProcessResult(process.ExitCode, await output, await error);
            }
        }
        static async Task FeedAsync(Stream pipe, byte[]? input, bool keepOpen, CancellationToken token) {
            try {
                if(input != null) {
                    await pipe.WriteAsync(input, token);
                    await pipe.FlushAsync(token);
                }
            }
            catch(IOException) {
                // Usage errors and input limits may close stdin before the producer has finished.
            }
            finally {
                if(!keepOpen)
                    pipe.Dispose();
            }
        }
    }
}
