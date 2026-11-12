using System.Buffers;
using System.IO;
using System.Threading;
using System;
using Fonts.Metadata.Internal;

namespace Fonts.Metadata {
    /// <summary>Reads SFNT and TTC metadata from the beginning of a supplied input. Does not search for embedded fonts.</summary>
    /// <remarks>
    /// Results own their data. Do not mutate input memory during a call. Options and limits are immutable.
    /// Cancellation and I/O exceptions propagate; they are never classified as malformed fonts.
    /// Complete means complete supported metadata, not validation of outlines, checksums or licensing.
    /// </remarks>
    public static class FontMetadataReader {
        const int StreamBufferSize = 80 * 1024;
        const string UnreadableStreamMessage = "The stream must be readable.";

        /// <summary>Inspects leading metadata and preserves verified partial results. Invalid arguments throw.</summary>
        public static FontInspectionResult Inspect(byte[] data, FontReadOptions? options = null, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(data);
            return Inspect(data.AsSpan(), options, cancellationToken);
        }
        /// <summary>Inspects leading metadata and preserves verified partial results. Invalid arguments throw.</summary>
        public static FontInspectionResult Inspect(byte[] data, int offset, int count, FontReadOptions? options = null, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(data);
            return Inspect(data.AsSpan(offset, count), options, cancellationToken);
        }
        /// <summary>Inspects leading metadata and preserves verified partial results. Invalid arguments throw.</summary>
        public static FontInspectionResult Inspect(ReadOnlyMemory<byte> data, FontReadOptions? options = null, CancellationToken cancellationToken = default) {
            return Inspect(data.Span, options, cancellationToken);
        }
        /// <summary>Inspects leading metadata without retaining the input span. Invalid arguments throw.</summary>
        public static FontInspectionResult Inspect(ReadOnlySpan<byte> data, FontReadOptions? options = null, CancellationToken cancellationToken = default) {
            FontReadLimits limits = (options ?? new FontReadOptions()).Validate();
            cancellationToken.ThrowIfCancellationRequested();
            if(data.Length > limits.MaxInputBytes)
                return SfntParser.InputLimit(data.Slice(0, limits.MaxInputBytes));
            return ContainerParser.Parse(data, limits, cancellationToken);
        }
        /// <summary>Inspects from the current stream position; never closes the stream and restores seekable positions in finally.</summary>
        /// <remarks>Non-seekable streams are consumed. Input beyond MaxInputBytes is not parsed. I/O and cancellation exceptions propagate.</remarks>
        public static FontInspectionResult Inspect(Stream stream, FontReadOptions? options = null, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(stream);
            if(!stream.CanRead)
                throw new ArgumentException(UnreadableStreamMessage, nameof(stream));
            FontReadLimits limits = (options ?? new FontReadOptions()).Validate();
            cancellationToken.ThrowIfCancellationRequested();
            long? position = stream.CanSeek ? stream.Position : null;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Min(StreamBufferSize, limits.MaxInputBytes));
            try {
                using(var content = new MemoryStream()) {
                    while(content.Length < limits.MaxInputBytes) {
                        cancellationToken.ThrowIfCancellationRequested();
                        int requested = (int)Math.Min(buffer.Length, limits.MaxInputBytes - content.Length);
                        int read = stream.Read(buffer, 0, requested);
                        cancellationToken.ThrowIfCancellationRequested();
                        if(read == 0) {
                            return InspectBufferedContent(content, limits, cancellationToken);
                        }
                        content.Write(buffer, 0, read);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    // Probe once at the exact limit; EOF is allowed, any extra byte means
                    // the entire input is oversized. Caller stream ownership is unchanged.
                    int extra = stream.ReadByte();
                    cancellationToken.ThrowIfCancellationRequested();
                    if(extra >= 0)
                        return SfntParser.InputLimit(content.GetBuffer().AsSpan(0, (int)content.Length));
                    return InspectBufferedContent(content, limits, cancellationToken);
                }
            }
            finally {
                ArrayPool<byte>.Shared.Return(buffer);
                if(position.HasValue)
                    stream.Position = position.Value;
            }
        }
        /// <summary>Opens and inspects a file, preserving partial results. Owns and closes only the file stream it opens.</summary>
        public static FontInspectionResult InspectFile(string path, FontReadOptions? options = null, CancellationToken cancellationToken = default) {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            (options ?? new FontReadOptions()).Validate();
            cancellationToken.ThrowIfCancellationRequested();
            using(var stream = File.OpenRead(path)) {
                return Inspect(stream, options, cancellationToken);
            }
        }
        /// <summary>Returns complete metadata, otherwise throws FontReadException or FontFeatureNotSupportedException with the inspection Result.</summary>
        public static FontMetadata Read(byte[] data, FontReadOptions? options = null, CancellationToken cancellationToken = default) {
            return RequireComplete(Inspect(data, options, cancellationToken));
        }
        /// <summary>Returns complete metadata, otherwise throws FontReadException or FontFeatureNotSupportedException with the inspection Result.</summary>
        public static FontMetadata Read(byte[] data, int offset, int count, FontReadOptions? options = null, CancellationToken cancellationToken = default) {
            return RequireComplete(Inspect(data, offset, count, options, cancellationToken));
        }
        /// <summary>Returns complete metadata, otherwise throws FontReadException or FontFeatureNotSupportedException with the inspection Result.</summary>
        public static FontMetadata Read(ReadOnlyMemory<byte> data, FontReadOptions? options = null, CancellationToken cancellationToken = default) {
            return RequireComplete(Inspect(data, options, cancellationToken));
        }
        /// <summary>Returns complete metadata, otherwise throws FontReadException or FontFeatureNotSupportedException with the inspection Result.</summary>
        public static FontMetadata Read(ReadOnlySpan<byte> data, FontReadOptions? options = null, CancellationToken cancellationToken = default) {
            return RequireComplete(Inspect(data, options, cancellationToken));
        }
        /// <summary>Returns complete metadata, otherwise throws FontReadException or FontFeatureNotSupportedException with the inspection Result.</summary>
        public static FontMetadata Read(Stream stream, FontReadOptions? options = null, CancellationToken cancellationToken = default) {
            return RequireComplete(Inspect(stream, options, cancellationToken));
        }
        /// <summary>Reads a file strictly; incomplete results remain available on the thrown exception. File I/O errors propagate.</summary>
        public static FontMetadata ReadFile(string path, FontReadOptions? options = null, CancellationToken cancellationToken = default) {
            return RequireComplete(InspectFile(path, options, cancellationToken));
        }
        /// <summary>Reports implemented readers, not whether a particular byte sequence is valid.</summary>
        public static bool IsFormatSupported(FontFormat format) {
            return format is FontFormat.Sfnt or FontFormat.Collection or FontFormat.Woff or FontFormat.Woff2 or FontFormat.Eot;
        }
        /// <summary>Recognizes common font filename extensions, without validating content.</summary>
        public static bool HasFontExtension(string path) {
            ArgumentNullException.ThrowIfNull(path);
            return Path.GetExtension(path).ToLowerInvariant() is ".ttf" or ".otf" or ".ttc" or ".otc" or ".woff" or ".woff2" or ".eot";
        }
        static FontInspectionResult InspectBufferedContent(MemoryStream content, FontReadLimits limits, CancellationToken token) {
            var bytes = content.GetBuffer().AsSpan(0, (int)content.Length);
            return ContainerParser.Parse(bytes, limits, token);
        }
        static FontMetadata RequireComplete(FontInspectionResult result) {
            if(result.Status == FontReadStatus.Complete)
                return result.Metadata!;
            if(result.Status == FontReadStatus.Unsupported)
                throw new FontFeatureNotSupportedException(result);
            throw new FontReadException(result);
        }
    }
}
