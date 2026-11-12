using System;
using System.IO;

namespace Fonts.Metadata.Internal {
    // Adapted from the independently tested SBOM/ILScan LZCOMP implementations.
    // Three streams share operation/payload work budgets; only complete stream 1 is retained.
    internal static class MtxDecoder {
        const int HeaderSize = 10, SecondOffset = 4, ThirdOffset = 7, StreamCount = 3;
        const byte Version = 3;
        internal sealed class Result : IDisposable {
            internal StreamData? First { get; set; }
            internal bool Complete { get; set; }
            internal bool Limited { get; set; }
            public void Dispose() {
                First?.Dispose();
            }
        }
        internal sealed class StreamData : IDisposable {
            internal StreamData(DecodedBuffer buffer, int offset, int length) {
                Buffer = buffer;
                Offset = offset;
                Length = length;
            }
            internal DecodedBuffer Buffer { get; }
            internal int Offset { get; }
            internal int Length { get; }
            internal ReadOnlySpan<byte> Content { get { return Buffer.Bytes.AsSpan(Offset, Length); } }
            public void Dispose() {
                Buffer.Dispose();
            }
        }
        internal static Result Decode(byte[] data, ReadingContext context, SourceLocation source, int face) {
            var result = new Result();
            if(data.Length < HeaderSize) {
                context.Error(FontDiagnosticCodes.InvalidCompressedData, ContainerMessages.Compression, source.Offset, face);
                return result;
            }
            int second = U24(data, SecondOffset), third = U24(data, ThirdOffset);
            if(data[0] != Version || second <= HeaderSize || third <= second || third >= data.Length) {
                context.Error(FontDiagnosticCodes.InvalidCompressedData, ContainerMessages.Compression, source.Offset, face);
                return result;
            }
            int[] offsets = { HeaderSize, second, third, data.Length };
            var payload = new PayloadWork();
            try {
                for(int index = 0; index < StreamCount; index++) {
                    context.CheckCancellation();
                    try {
                        var budget = new DecodeBudget(context, offsets[index + 1] - offsets[index], payload);
                        var decoded = LzCompDecoder.Decode(data, offsets[index], offsets[index + 1], context, budget);
                        if(index == 0)
                            result.First = decoded;
                        else
                            decoded.Dispose();
                    }
                    catch(DecodeLimitException) {
                        result.Limited = true;
                        context.Error(FontDiagnosticCodes.LimitExceeded, ContainerMessages.Limit, source.Map(offsets[index]), face,
                            category: FontDiagnosticCategory.LimitExceeded);
                        return result;
                    }
                    catch(Exception ex)
                        when(ex is InvalidDataException or OverflowException) {
                        context.Error(FontDiagnosticCodes.InvalidCompressedData, ContainerMessages.Compression, source.Map(offsets[index]), face);
                        return result;
                    }
                }
                result.Complete = true;
                return result;
            }
            catch {
                // Ownership cleanup only; cancellation/programming errors still propagate unchanged.
                result.Dispose();
                throw;
            }
        }
        static int U24(byte[] data, int offset) {
            return data[offset] << 16 | data[offset + 1] << 8 | data[offset + 2];
        }
        // MTX LZCOMP, bit order and adaptive Huffman rules:
        // https://www.w3.org/submissions/MTX/#appendixC
        static class LzCompDecoder {
            const int LiteralCount = 256, DistanceDigitBits = 3, DigitAlphabetSize = 1 << DistanceDigitBits;
            const int LengthRadix = 4, LengthValueMask = LengthRadix - 1, LengthContinuationBit = 4;
            const int OutputLengthBits = 24, MaximumDistanceRanges = OutputLengthBits / DistanceDigitBits;
            const int MinimumCopyLength = 2, LongDistanceThreshold = 512;
            const int Duplicate2Distance = 2, Duplicate4Distance = 4, Duplicate6Distance = 6;
            const int PreloadFirstValueCount = 32, PreloadSecondValueCount = 96, PreloadLiteralRepeat = 4;
            const int PreloadSize = 2 * PreloadFirstValueCount * PreloadSecondValueCount + PreloadLiteralRepeat * LiteralCount;
            const int MaximumLengthChunks = 16;
            internal static StreamData Decode(byte[] data, int start, int end, ReadingContext context, DecodeBudget budget) {
                var bits = new BitReader(data, start, end, context.CheckCancellation);
                bool runLength = bits.ReadBit();
                var distanceTree = new AdaptiveHuffman(bits, DigitAlphabetSize);
                var lengthTree = new AdaptiveHuffman(bits, DigitAlphabetSize);
                int outputLength = bits.ReadValue(OutputLengthBits);
                budget.CheckDeclared(outputLength);
                int distanceRanges = 1;
                long maximumDistance = 1 + ((1L << (DistanceDigitBits * distanceRanges)) - 1);
                while(maximumDistance < outputLength) {
                    if(++distanceRanges > MaximumDistanceRanges)
                        throw new InvalidDataException();
                    maximumDistance = 1 + ((1L << (DistanceDigitBits * distanceRanges)) - 1);
                }
                int duplicate2 = LiteralCount + DigitAlphabetSize * distanceRanges;
                int duplicate4 = duplicate2 + 1, duplicate6 = duplicate4 + 1;
                var symbolTree = new AdaptiveHuffman(bits, duplicate6 + 1);
                var window = new DecodedBuffer(context, PreloadSize + outputLength);
                RleOutput? rle = null;
                bool transferred = false;
                try {
                    InitializeWindow(window.Bytes);
                    if(runLength)
                        rle = new RleOutput(context, budget);
                    int position = 0;
                    while(position < outputLength) {
                        int symbol = symbolTree.ReadSymbol();
                        if(symbol < LiteralCount) {
                            Emit(symbol);
                            continue;
                        }
                        if(symbol == duplicate2 || symbol == duplicate4 || symbol == duplicate6) {
                            int distance = symbol == duplicate2 ? Duplicate2Distance :
                                symbol == duplicate4 ? Duplicate4Distance : Duplicate6Distance;
                            Emit(window.Bytes[PreloadSize + position - distance]);
                            continue;
                        }
                        int encodedLengthBits = symbol - LiteralCount;
                        int copyRanges = encodedLengthBits / DigitAlphabetSize + 1;
                        if(copyRanges < 1 || copyRanges > distanceRanges)
                            throw new InvalidDataException();
                        int length = 0, chunk = encodedLengthBits % DigitAlphabetSize, iterations = 0;
                        while(true) {
                            if(++iterations > MaximumLengthChunks)
                                throw new InvalidDataException();
                            length = checked(length * LengthRadix + (chunk & LengthValueMask));
                            if((chunk & LengthContinuationBit) == 0)
                                break;
                            chunk = lengthTree.ReadSymbol();
                        }
                        length = checked(length + MinimumCopyLength);
                        int distanceValue = 0;
                        for(int i = 0; i < copyRanges; i++)
                            distanceValue = checked(distanceValue * DigitAlphabetSize + distanceTree.ReadSymbol());
                        distanceValue++;
                        if(distanceValue >= LongDistanceThreshold)
                            length++;
                        if(length <= 0 || (long)position + length > outputLength)
                            throw new InvalidDataException();
                        // Distance is measured from the end of the copied span, including the preloaded dictionary.
                        int source = PreloadSize + position - distanceValue - length + 1;
                        if(source < 0)
                            throw new InvalidDataException();
                        for(int i = 0; i < length; i++)
                            Emit(window.Bytes[source + i]);
                    }
                    bits.EnsureEnd();
                    if(rle != null)
                        return rle.Finish();
                    transferred = true;
                    return new StreamData(window, PreloadSize, outputLength);
                    void Emit(int value) {
                        context.CheckCancellation();
                        if(position >= outputLength)
                            throw new InvalidDataException();
                        budget.CheckDeclared(1);
                        byte item = (byte)value;
                        window.Bytes[PreloadSize + position++] = item;
                        budget.Charge(1);
                        rle?.Emit(item);
                    }
                }
                finally {
                    rle?.Dispose();
                    if(!transferred)
                        window.Dispose();
                }
            }
            static void InitializeWindow(byte[] window) {
                int position = 0;
                for(int first = 0; first < PreloadFirstValueCount; first++) {
                    for(int second = 0; second < PreloadSecondValueCount; second++) {
                        window[position++] = (byte)first;
                        window[position++] = (byte)second;
                    }
                }
                for(int value = 0; value < LiteralCount; value++) {
                    for(int count = 0; count < PreloadLiteralRepeat; count++)
                        window[position++] = (byte)value;
                }
            }
            sealed class RleOutput : IDisposable {
                enum State { Escape, Literal, Count, Value }
                readonly ReadingContext context;
                readonly DecodeBudget budget;
                DecodedBuffer? buffer;
                int position, repeat;
                byte escape;
                State state;
                internal RleOutput(ReadingContext context, DecodeBudget budget) {
                    this.context = context;
                    this.budget = budget;
                    buffer = new DecodedBuffer(context, 0);
                }
                internal void Emit(byte value) {
                    switch(state) {
                        case State.Escape:
                            escape = value;
                            state = State.Literal;
                            break;
                        case State.Literal:
                            if(value == escape)
                                state = State.Count;
                            else Append(value, 1);
                            break;
                        case State.Count:
                            repeat = value;
                            if(repeat == 0) {
                                Append(escape, 1);
                                state = State.Literal;
                            }
                            else state = State.Value;
                            break;
                        case State.Value:
                            Append(value, repeat);
                            state = State.Literal;
                            break;
                    }
                }
                void Append(byte value, int count) {
                    budget.CheckDeclared(count);
                    int required = checked(position + count);
                    if(required > buffer!.Bytes.Length) {
                        long available = context.Limits.MaxRetainedDecodedBytes - context.RetainedBytes;
                        long preferred = Math.Max(required, Math.Max(1024L, 2L * buffer.Bytes.Length));
                        int capacity = (int)Math.Max(required, Math.Min(available, preferred));
                        buffer.Resize(capacity);
                    }
                    context.CheckCancellation();
                    buffer.Bytes.AsSpan(position, count).Fill(value);
                    position += count;
                    budget.Charge(count);
                }
                internal StreamData Finish() {
                    if(state != State.Literal)
                        throw new InvalidDataException();
                    var result = new StreamData(buffer!, 0, position);
                    buffer = null;
                    return result;
                }
                public void Dispose() {
                    buffer?.Dispose();
                }
            }
            sealed class BitReader {
                // Bytes are consumed MSB first; shifting moves the next bit into bit 8.
                const int BitsPerByte = 8, ShiftedBitMask = 1 << BitsPerByte;
                readonly byte[] data;
                readonly int end;
                readonly Action checkCancellation;
                int position;
                int bitBuffer;
                int bitCount;
                internal BitReader(byte[] data, int start, int end, Action checkCancellation) {
                    this.checkCancellation = checkCancellation;
                    if(start < 0 || end < start || end > data.Length)
                        throw new ArgumentOutOfRangeException();
                    this.data = data;
                    position = start;
                    this.end = end;
                }
                internal bool ReadBit() {
                    if(bitCount == 0) {
                        if((position & 4095) == 0)
                            checkCancellation();
                        if(position >= end)
                            throw new InvalidDataException();
                        bitBuffer = data[position++];
                        bitCount = BitsPerByte;
                    }
                    bitCount--;
                    bitBuffer <<= 1;
                    return (bitBuffer & ShiftedBitMask) != 0;
                }
                internal void EnsureEnd() {
                    // MTX Appendix C, MTX_BITIO_flush_bits: only the final partial byte is zero-padded, never whole bytes.
                    // ReadBit shifts consumed bits out of the low byte; e.g. three unread zero bits leave that byte zero.
                    if(position != end || (bitBuffer & 0xff) != 0)
                        throw new InvalidDataException();
                }
                internal int ReadValue(int count) {
                    int value = 0;
                    for(int i = 0; i < count; i++)
                        value = (value << 1) | (ReadBit() ? 1 : 0);
                    return value;
                }
            }
            sealed class AdaptiveHuffman {
                const int Root = 1;
                // Array index 0 is a sentinel; internal nodes have Code=-1, leaves hold symbol codes.
                const int MaximumAlphabetSizeExclusive = 512;
                // Initial symbol bias is part of MTX decoding: two first copy symbols, then DUP2/DUP4.
                const int Duplicate2BiasUpdates = 12, Duplicate4BiasUpdates = 6, SmallAlphabetBiasPasses = 2;
                readonly BitReader bits;
                readonly Node[] tree;
                readonly int[] symbolIndices;
                internal AdaptiveHuffman(BitReader bits, int range) {
                    if(range < 2 || range >= MaximumAlphabetSizeExclusive)
                        throw new ArgumentOutOfRangeException(nameof(range));
                    this.bits = bits;
                    tree = new Node[2 * range];
                    for(int i = 2; i < tree.Length; i++) {
                        tree[i].Up = i >> 1;
                        tree[i].Weight = 1;
                    }
                    for(int i = 1; i < range; i++) {
                        tree[i].Left = 2 * i;
                        tree[i].Right = 2 * i + 1;
                        tree[i].Code = -1;
                    }
                    symbolIndices = new int[range];
                    for(int symbol = 0; symbol < range; symbol++) {
                        int leaf = range + symbol;
                        tree[leaf].Left = -1;
                        tree[leaf].Right = -1;
                        tree[leaf].Code = symbol;
                        symbolIndices[symbol] = leaf;
                    }
                    InitializeWeight(Root);
                    if(range > LiteralCount) {
                        UpdateWeight(symbolIndices[LiteralCount]);
                        UpdateWeight(symbolIndices[LiteralCount + 1]);
                        for(int i = 0; i < Duplicate2BiasUpdates; i++)
                            UpdateWeight(symbolIndices[range - 3]);
                        for(int i = 0; i < Duplicate4BiasUpdates; i++)
                            UpdateWeight(symbolIndices[range - 2]);
                    }
                    else {
                        for(int repeat = 0; repeat < SmallAlphabetBiasPasses; repeat++) {
                            for(int symbol = 0; symbol < range; symbol++)
                                UpdateWeight(symbolIndices[symbol]);
                        }
                    }
                }
                internal int ReadSymbol() {
                    int node = Root;
                    int steps = 0;
                    while(tree[node].Code < 0) {
                        if(++steps > tree.Length)
                            throw new InvalidDataException();
                        node = bits.ReadBit() ? tree[node].Right : tree[node].Left;
                        if(node <= 0 || node >= tree.Length)
                            throw new InvalidDataException();
                    }
                    int symbol = tree[node].Code;
                    UpdateWeight(node);
                    return symbol;
                }
                int InitializeWeight(int node) {
                    if(tree[node].Code >= 0)
                        return tree[node].Weight;
                    tree[node].Weight = InitializeWeight(tree[node].Left) + InitializeWeight(tree[node].Right);
                    return tree[node].Weight;
                }
                void UpdateWeight(int node) {
                    // Preserve the adaptive tree's weight ordering by swapping equal-weight nodes
                    // before incrementing the path to the root; FixLinks repairs moved children/leaves.
                    int steps = 0;
                    while(node != Root) {
                        if(++steps > tree.Length)
                            throw new InvalidDataException();
                        int weight = tree[node].Weight;
                        int candidate = node - 1;
                        if(tree[candidate].Weight == weight) {
                            do {
                                candidate--;
                            } while(candidate >= 0 && tree[candidate].Weight == weight);
                            candidate++;
                            if(candidate > Root) {
                                SwapNodes(node, candidate);
                                node = candidate;
                            }
                        }
                        tree[node].Weight = checked(weight + 1);
                        node = tree[node].Up;
                    }
                    tree[Root].Weight = checked(tree[Root].Weight + 1);
                }
                void SwapNodes(int first, int second) {
                    int firstParent = tree[first].Up;
                    int secondParent = tree[second].Up;
                    Node temporary = tree[first];
                    tree[first] = tree[second];
                    tree[second] = temporary;
                    tree[first].Up = firstParent;
                    tree[second].Up = secondParent;
                    FixLinks(first);
                    FixLinks(second);
                }
                void FixLinks(int node) {
                    if(tree[node].Code < 0) {
                        tree[tree[node].Left].Up = node;
                        tree[tree[node].Right].Up = node;
                    }
                    else symbolIndices[tree[node].Code] = node;
                }
                struct Node {
                    internal int Up;
                    internal int Left;
                    internal int Right;
                    internal int Code;
                    internal int Weight;
                }
            }
        }
    }
}
