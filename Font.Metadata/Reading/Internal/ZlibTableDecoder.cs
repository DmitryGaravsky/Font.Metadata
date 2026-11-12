using System;
using System.IO;
using System.IO.Compression;

namespace Fonts.Metadata.Internal {
    internal static class ZlibTableDecoder {
        internal static void Decode(byte[] input, byte[] output, ReadingContext context) {
            var budget = new DecodeBudget(context, input.Length);
            budget.CheckDeclared(output.Length);
            // Stream APIs do not expose inflater-consumed input or completion. Supplying one byte
            // at a time prevents read-ahead; throwing on an input over-read distinguishes truncated
            // trailers from completion without changing the host's strict-validation switch.
            // https://www.rfc-editor.org/rfc/rfc1950#section-2.2
            using(var source = new ExactInput(input, context)) {
                using(var decoder = new ZLibStream(source, CompressionMode.Decompress, leaveOpen: true)) {
                    var scratch = new byte[ReadingContext.CopyChunkSize];
                    int offset = 0;
                    while(true) {
                        context.CheckCancellation();
                        int count = decoder.Read(scratch, 0, budget.OutputSize(scratch.Length));
                        budget.Charge(count);
                        if(count == 0)
                            break;
                        if(count > output.Length - offset)
                            throw new InvalidDataException(ContainerMessages.Compression);
                        scratch.AsSpan(0, count).CopyTo(output.AsSpan(offset));
                        offset += count;
                    }
                    if(offset != output.Length || source.Consumed != input.Length)
                        throw new InvalidDataException(ContainerMessages.Compression);
                }
            }
        }
        sealed class ExactInput : Stream {
            readonly byte[] bytes;
            readonly ReadingContext context;
            int position;
            internal ExactInput(byte[] bytes, ReadingContext context) {
                this.bytes = bytes;
                this.context = context;
            }
            internal int Consumed { get { return position; } }
            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { return bytes.Length; } }
            public override long Position {
                get { return position; }
                set { throw new NotSupportedException(); }
            }
            public override int Read(byte[] buffer, int offset, int count) {
                return Read(buffer.AsSpan(offset, count));
            }
            public override int Read(Span<byte> buffer) {
                context.CheckCancellation();
                if(buffer.IsEmpty)
                    return 0;
                if(position == bytes.Length)
                    throw new InvalidDataException(ContainerMessages.Compression);
                buffer[0] = bytes[position++];
                return 1;
            }
            public override void Flush() {
            }
            public override long Seek(long offset, SeekOrigin origin) {
                throw new NotSupportedException();
            }
            public override void SetLength(long value) {
                throw new NotSupportedException();
            }
            public override void Write(byte[] buffer, int offset, int count) {
                throw new NotSupportedException();
            }
        }
    }
}
