using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spdx.Expressions;

namespace Fonts.Metadata.Tool {
    internal static class LicenseDocumentCommand {
        internal const int MaxInputBytes = 1024 * 1024;
        const int Unbuffered = 1;
        const string InputLimitMessage = "The document exceeds the input byte limit.";
        const string InvalidUtf8Message = "The document contains invalid UTF-8.";
        static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        internal static async Task<int> RunAsync(CommandOptions command, Stream output, SpdxLicenseRegistry registry, CancellationToken token) {
            var options = new FontLicenseDocumentOptions();
            using(var report = new LicenseDocumentReport(output, command.Json, options, registry.Identity, token)) {
                report.Begin();
                byte[] bytes = new byte[MaxInputBytes + 1];
                int exitCode = 0;
                foreach(var source in command.Sources) {
                    token.ThrowIfCancellationRequested();
                    var result = await InspectAsync(source.Path!, bytes, registry, options, token);
                    report.Write(result);
                    exitCode = Math.Max(exitCode, result.Status switch {
                        LicenseDocumentStatus.Complete => 0,
                        LicenseDocumentStatus.IoError => 3,
                        _ => 1
                    });
                }
                report.End();
                return exitCode;
            }
        }
        static async Task<LicenseDocumentResult> InspectAsync(string path, byte[] bytes, SpdxLicenseRegistry registry,
            FontLicenseDocumentOptions options, CancellationToken token) {
            int count;
            try {
                // The command's buffer owns the limit and the single-byte overflow probe.
                using(var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, Unbuffered, useAsync: true)) {
                    count = await stream.ReadAtLeastAsync(bytes.AsMemory(), bytes.Length, throwOnEndOfStream: false, cancellationToken: token);
                }
            }
            catch(Exception exception)
                when(exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) {
                return new LicenseDocumentResult(path, LicenseDocumentStatus.IoError,
                    Code: "CLI_IO_ERROR", Message: exception.Message);
            }
            token.ThrowIfCancellationRequested();
            if(count > MaxInputBytes) {
                return new LicenseDocumentResult(path, LicenseDocumentStatus.LimitExceeded,
                    Code: "CLI_LICENSE_INPUT_LIMIT", Message: InputLimitMessage);
            }
            var content = bytes.AsSpan(0, count);
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
            bool bom = content.StartsWith("\uFEFF"u8);
            string text;
            try {
                // Decode the BOM as U+FEFF so UTF-16 coordinates include it. The analyzer ignores it as framing.
                text = StrictUtf8.GetString(content);
            }
            catch(DecoderFallbackException) {
                return new LicenseDocumentResult(path, LicenseDocumentStatus.InvalidEncoding, sha256, bom,
                    Code: "CLI_LICENSE_INVALID_UTF8", Message: InvalidUtf8Message);
            }
            var analysis = FontLicenseDocumentAnalyzer.Analyze(text, registry, options, token);
            var status = analysis.IsAnalysisComplete ? LicenseDocumentStatus.Complete : LicenseDocumentStatus.LimitExceeded;
            return new LicenseDocumentResult(path, status, sha256, bom, analysis);
        }
    }
    internal enum LicenseDocumentStatus { Complete, LimitExceeded, InvalidEncoding, IoError }
    internal sealed record LicenseDocumentResult(string Path, LicenseDocumentStatus Status, string? Sha256 = null,
        bool? HasUtf8Bom = null, FontLicenseDocumentAnalysis? Analysis = null, string? Code = null, string? Message = null);
}
