using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fonts.Metadata.Tool {
    internal static class Command {
        internal const string Usage = "Usage: font-metadata inspect <path> [<path> ...] [options]\n       font-metadata inspect - [options]\n       font-metadata --help | --version";
        const string Help = "Font.Metadata.Tool\n" + Usage +
            "\nOptions: --format plain|json (default: plain), --include-name-records, --analyze-licenses, --max-input-bytes <bytes>\n" +
            "Use -- before literal paths beginning with '-'. Stdin '-' must be the only source.\n" +
            "Reads SFNT/TTC, WOFF, WOFF2 and EOT. No embedded search or directory traversal.\n" +
            "Input defaults to 128 MiB; additional library decoding limits apply (see docs/cli.md).\n" +
            "License hints are evidence, not a license decision.\n" +
            "Exit codes: 0 complete, 1 incomplete/unrecognized, 2 arguments, 3 I/O, 130 cancellation.";
        const string InvalidArguments = "CLI_INVALID_ARGUMENTS: Invalid arguments. Use --help.";
        const string Cancelled = "CLI_CANCELLED: Inspection cancelled; the output may be incomplete.";
        const string OutputError = "CLI_OUTPUT_ERROR: Could not write the report; the output may be incomplete.";
        const int BufferSize = 81920;

        internal static Task<int> RunAsync(string[] args, TextWriter error, CancellationToken token) {
            return RunAsync(args, options => ExecuteConsoleAsync(options, token), error, token);
        }
        internal static Task<int> RunAsync(string[] args, Stream input, Stream output, TextWriter error, CancellationToken token) {
            return RunAsync(args, options => ExecuteAsync(options, input, output, token), error, token);
        }
        static async Task<int> RunAsync(string[] args, Func<CommandOptions, Task<int>> execute, TextWriter error, CancellationToken token) {
            if(!CommandOptions.TryParse(args, out var options)) {
                WriteError(error, InvalidArguments + "\n" + Usage);
                return 2;
            }
            try {
                token.ThrowIfCancellationRequested();
                int exitCode = await execute(options);
                token.ThrowIfCancellationRequested();
                return exitCode;
            }
            catch(OperationCanceledException)
                when(token.IsCancellationRequested) {
                WriteError(error, Cancelled);
                return 130;
            }
            catch(Exception exception)
                when(exception is IOException or UnauthorizedAccessException) {
                WriteError(error, token.IsCancellationRequested ? Cancelled : OutputError);
                return token.IsCancellationRequested ? 130 : 3;
            }
        }
        static async Task<int> ExecuteConsoleAsync(CommandOptions options, CancellationToken token) {
            using(var input = Console.OpenStandardInput())
            using(var output = StandardOutput.Open()) {
                return await ExecuteAsync(options, input, output, token);
            }
        }
        static async Task<int> ExecuteAsync(CommandOptions options, Stream input, Stream output, CancellationToken token) {
            if(options.Help || options.Version) {
                string? version = typeof(Command).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                byte[] text = Encoding.UTF8.GetBytes((options.Help ? Help : version) + Environment.NewLine);
                output.Write(text);
                output.Flush();
                return 0;
            }
            var limits = new FontReadLimits { MaxInputBytes = options.MaxInputBytes };
            var readOptions = new FontReadOptions { Limits = limits };
            using(ReportWriter writer = options.Json ?
                new JsonReportWriter(output, options, token) : new PlainReportWriter(output, options, token)) {
                writer.Begin();
                bool incomplete = false, ioError = false;
                foreach(var source in options.Sources) {
                    token.ThrowIfCancellationRequested();
                    SourceResult result = await InspectAsync(source, input, readOptions, token);
                    token.ThrowIfCancellationRequested();
                    ioError |= result.Error != null;
                    incomplete |= result.Inspection?.Status != FontReadStatus.Complete;
                    writer.Write(result);
                }
                token.ThrowIfCancellationRequested();
                writer.End();
                return ioError ? 3 : incomplete ? 1 : 0;
            }
        }
        static void WriteError(TextWriter error, string message) {
            try {
                error.WriteLine(message);
                error.Flush();
            }
            catch(IOException) {
                // The error channel can also be closed, for example by a pipeline consumer.
            }
        }
        static async Task<SourceResult> InspectAsync(InputSource source, Stream input, FontReadOptions options, CancellationToken token) {
            try {
                FontInspectionResult inspection;
                if(source.Path == null) {
                    inspection = await InspectInputAsync(input, options, token);
                }
                else {
                    FileStream stream;
                    try {
                        stream = File.OpenRead(source.Path);
                    }
                    catch(Exception exception)
                        when(exception is ArgumentException or NotSupportedException) {
                        return new SourceResult(source, null, exception.Message);
                    }
                    using(stream) {
                        inspection = FontMetadataReader.Inspect(stream, options, token);
                    }
                }
                return new SourceResult(source, inspection, null);
            }
            catch(Exception exception)
                when(exception is IOException or UnauthorizedAccessException) {
                return new SourceResult(source, null, exception.Message);
            }
        }
        static async Task<FontInspectionResult> InspectInputAsync(Stream input, FontReadOptions options, CancellationToken token) {
            int maximum = options.Limits.MaxInputBytes + 1;
            using(var content = new MemoryStream()) {
                byte[] buffer = new byte[Math.Min(BufferSize, maximum)];
                while(content.Length < maximum) {
                    token.ThrowIfCancellationRequested();
                    int request = (int)Math.Min(buffer.Length, maximum - content.Length);
                    int read = await input.ReadAsync(buffer.AsMemory(0, request), token);
                    token.ThrowIfCancellationRequested();
                    if(read == 0)
                        break;
                    int required = checked((int)content.Length + read);
                    if(required > content.Capacity) {
                        int capacity = (int)Math.Min(maximum, Math.Max((long)required, (long)content.Capacity * 2));
                        content.Capacity = capacity;
                    }
                    content.Write(buffer, 0, read);
                }
                return FontMetadataReader.Inspect(content.GetBuffer().AsMemory(0, (int)content.Length), options, token);
            }
        }
    }

    internal sealed record SourceResult(InputSource Source, FontInspectionResult? Inspection, string? Error);
}
