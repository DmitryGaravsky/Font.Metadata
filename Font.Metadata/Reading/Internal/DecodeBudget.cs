using System;

namespace Fonts.Metadata.Internal {
    internal sealed class DecodeLimitException : Exception {
        internal DecodeLimitException() {
        }
    }
    internal sealed class PayloadWork {
        internal long Bytes { get; set; }
    }
    internal sealed class DecodeBudget {
        readonly ReadingContext context;
        readonly PayloadWork? payload;
        readonly long ratioLimit;
        long bytes;
        internal DecodeBudget(ReadingContext context, int compressedLength, PayloadWork? payload = null) {
            this.context = context;
            this.payload = payload;
            ratioLimit = (long)compressedLength * context.Limits.MaxExpansionRatio;
        }
        internal long Remaining {
            get {
                long remaining = Math.Min(context.Limits.MaxTotalDecodedBytes - context.DecodedBytes, ratioLimit - bytes);
                if(payload != null)
                    remaining = Math.Min(remaining, context.Limits.MaxMtxPayloadBytes - payload.Bytes);
                return Math.Max(0, remaining);
            }
        }
        internal void CheckDeclared(long length) {
            context.CheckCancellation();
            if(length < 0 || length > Remaining)
                throw new DecodeLimitException();
        }
        internal void Charge(int count) {
            context.DecodedBytes += count;
            bytes += count;
            if(payload != null)
                payload.Bytes += count;
            context.CheckCancellation();
            if(context.DecodedBytes > context.Limits.MaxTotalDecodedBytes || bytes > ratioLimit ||
                (payload != null && payload.Bytes > context.Limits.MaxMtxPayloadBytes))
                throw new DecodeLimitException();
        }
        internal int OutputSize(int capacity) {
            // One bounded probe distinguishes exact completion from output beyond a limit.
            return (int)Math.Min(capacity, Remaining + 1);
        }
    }
    internal sealed class DecodedBuffer : IDisposable {
        readonly ReadingContext context;
        bool disposed;
        internal DecodedBuffer(ReadingContext context, int length) {
            this.context = context;
            context.ReserveRetained(length);
            Bytes = new byte[length];
        }
        internal byte[] Bytes { get; private set; }
        internal void Resize(int length) {
            context.ReserveRetained(length);
            bool transferred = false;
            try {
                var replacement = new byte[length];
                int count = Math.Min(length, Bytes.Length);
                for(int start = 0; start < count;) {
                    context.CheckCancellation();
                    int size = Math.Min(ReadingContext.CopyChunkSize, count - start);
                    Bytes.AsSpan(start, size).CopyTo(replacement.AsSpan(start));
                    start += size;
                }
                context.ReleaseRetained(Bytes.Length);
                Bytes = replacement;
                transferred = true;
            }
            finally {
                if(!transferred)
                    context.ReleaseRetained(length);
            }
        }
        public void Dispose() {
            if(disposed)
                return;
            disposed = true;
            context.ReleaseRetained(Bytes.Length);
            Bytes = Array.Empty<byte>();
        }
    }
}
