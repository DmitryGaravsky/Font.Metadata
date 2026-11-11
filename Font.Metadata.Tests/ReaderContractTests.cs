using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class ReaderContractTests {
        [Test]
        public void ArraySliceMemoryAndSpanUseTheSameInputOrigin() {
            byte[] font = Example();
            byte[] prefix = new byte[13].Concat(font).ToArray();
            var array = FontMetadataReader.Read(prefix, 13, font.Length);
            var memory = FontMetadataReader.Read(new ReadOnlyMemory<byte>(prefix, 13, font.Length));
            var span = FontMetadataReader.Read(prefix.AsSpan(13));
            Assert.That(new[] { array.Faces[0].FamilyName, memory.Faces[0].FamilyName, span.Faces[0].FamilyName },
                Is.All.EqualTo("Example"));
            Assert.That(FontMetadataReader.Inspect(prefix).Status, Is.EqualTo(FontReadStatus.NotRecognized));
            U32(font, TableOffsetPosition(1), uint.MaxValue);
            font.CopyTo(prefix, 13);
            var result = FontMetadataReader.Inspect(prefix, 13, font.Length);
            Assert.That(result.Diagnostics[0].ByteOffset, Is.EqualTo(36));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void SeekableStreamStartsAtCurrentPositionAndIsRestored(bool malformed) {
            byte[] font = malformed ? Sfnt(("name", new byte[1])) : Example();
            using(var stream = new MemoryStream(new byte[7].Concat(font).ToArray())) {
                stream.Position = 7;
                var result = FontMetadataReader.Inspect(stream);
                Assert.That(result.Status, Is.EqualTo(malformed ? FontReadStatus.Failed : FontReadStatus.Complete));
                Assert.That(stream.Position, Is.EqualTo(7));
                Assert.That(stream.CanRead, Is.True);
                if(malformed)
                    Assert.Throws<FontReadException>(() => FontMetadataReader.Read(stream));
                else
                    Assert.That(FontMetadataReader.Read(stream).Faces[0].FamilyName, Is.EqualTo("Example"));
                Assert.That(stream.Position, Is.EqualTo(7));
            }
        }
        [Test]
        public void NonSeekableShortReadsAreConsumedWithoutClosingTheStream() {
            using(var stream = new ControlledStream(Example(), seekable: false)) {
                Assert.That(FontMetadataReader.Read(stream).Faces[0].FamilyName, Is.EqualTo("Example"));
                Assert.That(stream.BytesRead, Is.EqualTo(Example().Length));
                Assert.That(stream.Disposed, Is.False);
            }
        }
        [Test]
        public void InputLimitReadsAtMostOneProbeByteAndRestoresPosition() {
            using(var stream = new ControlledStream(Example())) {
                var result = FontMetadataReader.Inspect(stream,
                    new FontReadOptions { Limits = new FontReadLimits { MaxInputBytes = 8 } });
                Assert.That(result.Status, Is.EqualTo(FontReadStatus.Failed));
                Assert.That(stream.BytesRead, Is.EqualTo(9));
                Assert.That(stream.Position, Is.Zero);
                Assert.That(stream.Disposed, Is.False);
            }
            byte[] font = Example();
            using(var stream = new MemoryStream(font)) {
                var options = new FontReadOptions { Limits = new FontReadLimits { MaxInputBytes = font.Length } };
                Assert.That(FontMetadataReader.Inspect(stream, options).Status,
                    Is.EqualTo(FontReadStatus.Complete));
            }
        }
        [Test]
        public void CancellationAndIoErrorsPropagateAndRestorePosition() {
            using(var cancellation = new CancellationTokenSource())
            using(var stream = new ControlledStream(Example(), afterRead: cancellation.Cancel)) {
                Assert.Throws<OperationCanceledException>(() => FontMetadataReader.Inspect(stream, cancellationToken: cancellation.Token));
                Assert.That(stream.Position, Is.Zero);
                Assert.That(stream.Disposed, Is.False);
            }
            using(var stream = new ControlledStream(Example(), throwIo: true)) {
                Assert.Throws<IOException>(() => FontMetadataReader.Inspect(stream));
                Assert.That(stream.Position, Is.Zero);
                Assert.That(stream.Disposed, Is.False);
            }
            Assert.Throws<OperationCanceledException>(() => FontMetadataReader.Inspect(Example(), cancellationToken: new CancellationToken(true)));
        }
        [Test]
        public void FileOverloadsOwnTheirStreamsAndPropagateMissingFileErrors() {
            string path = Path.GetTempFileName();
            try {
                File.WriteAllBytes(path, Example());
                Assert.That(FontMetadataReader.ReadFile(path).Faces[0].FamilyName, Is.EqualTo("Example"));
                Assert.That(FontMetadataReader.InspectFile(path).Status, Is.EqualTo(FontReadStatus.Complete));
                using(var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                    Assert.That(exclusive.Length, Is.GreaterThan(0));
                }
            }
            finally { File.Delete(path); }
            Assert.Throws<FileNotFoundException>(() => FontMetadataReader.InspectFile(path));
        }
        [Test]
        public void InvalidArgumentsAndLimitsAreNotMalformedFontResults() {
            Assert.Throws<ArgumentNullException>(() => FontMetadataReader.Inspect((byte[])null!));
            Assert.Throws<ArgumentNullException>(() => FontMetadataReader.Inspect((Stream)null!));
            Assert.Throws<ArgumentException>(() => FontMetadataReader.InspectFile(" "));
            Assert.Throws<ArgumentOutOfRangeException>(() => FontMetadataReader.Inspect(new byte[4], -1, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => FontMetadataReader.Inspect(new byte[4], 2, 3));
            Assert.Throws<ArgumentNullException>(() => FontMetadataReader.Inspect(Example(), new FontReadOptions { Limits = null! }));
            var sampleLimits = new[] {
                new FontReadLimits { MaxInputBytes = 0 }, new FontReadLimits { MaxTableBytes = 0 },
                new FontReadLimits { MaxFaces = 0 }, new FontReadLimits { MaxTablesPerFace = 0 },
                new FontReadLimits { MaxNameRecords = 0 }, new FontReadLimits { MaxNameStringBytes = 0 },
                new FontReadLimits { MaxTotalNameStringBytes = 0 }, new FontReadLimits { MaxDiagnostics = 0 }
            };
            foreach(var limits in sampleLimits) {
                var options = new FontReadOptions { Limits = limits };
                Assert.Throws<ArgumentOutOfRangeException>(() => FontMetadataReader.Inspect(Example(), options));
            }
        }
        [Test]
        public void ResultsDoNotRetainMutableInputOrExposeMutableCollections() {
            byte[] input = Example();
            var metadata = FontMetadataReader.Read(input);
            Array.Clear(input);
            Assert.That(metadata.Faces[0].FamilyName, Is.EqualTo("Example"));
            var faces = (IList<FontMetadata.Face>)metadata.Faces;
            var records = (IList<FontMetadata.NameRecord>)metadata.Faces[0].NameRecords;
            Assert.Throws<NotSupportedException>(() => faces.Clear());
            Assert.Throws<NotSupportedException>(() => records.Clear());
        }
        [Test]
        public void EmptyCollectionsRemainReadOnlyForUnavailableMetadata() {
            byte[][] inputs = [
                Convert.FromHexString("00010000"),
                Sfnt(("OS/2", Os2())),
                Sfnt(("name", new byte[1])),
                Sfnt(("name", Names()))
            ];
            foreach(byte[] input in inputs) {
                var result = FontMetadataReader.Inspect(input);
                var face = result.Metadata!.Faces[0];
                Assert.That(face.NameRecords, Is.Empty);
                Assert.That(face.FamilyName, Is.Null);
                Assert.Throws<NotSupportedException>(() => ((IList<FontMetadata.NameRecord>)face.NameRecords).Clear());
            }
            var limited = FontMetadataReader.Inspect(Example(), new FontReadOptions { Limits = new FontReadLimits { MaxInputBytes = 4 } });
            Assert.That(limited.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(limited.Metadata!.Faces, Is.Empty);
            Assert.Throws<NotSupportedException>(() => ((IList<FontMetadata.Face>)limited.Metadata.Faces).Clear());
            var unknown = FontMetadataReader.Inspect(Array.Empty<byte>());
            Assert.That(unknown.Diagnostics, Is.Empty);
            Assert.Throws<NotSupportedException>(() => ((IList<FontDiagnostic>)unknown.Diagnostics).Clear());
        }
        [TestCase("774F46")]
        [TestCase("574F4646")]
        [TestCase("774F6632")]
        [TestCase("54544346")]
        public void TextSignaturesRequireExactFourByteValues(string hex) {
            var result = FontMetadataReader.Inspect(Convert.FromHexString(hex));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.NotRecognized));
            Assert.That(result.Metadata, Is.Null);
        }
        [Test]
        public void ConcurrentReadsHaveIndependentBudgets() {
            byte[] input = Example();
            Parallel.For(0, 40, index => {
                var options = index % 2 == 0 ? null : new FontReadOptions { Limits = new FontReadLimits { MaxNameStringBytes = 1 } };
                var result = FontMetadataReader.Inspect(input, options);
                Assert.That(result.Status, Is.EqualTo(index % 2 == 0 ? FontReadStatus.Complete : FontReadStatus.Partial));
            });
        }
        [TestCase("774F4646", FontFormat.Woff)]
        [TestCase("774F4632", FontFormat.Woff2)]
        public void RecognizedFutureContainersAreExplicitlyUnsupported(string hex, FontFormat format) {
            byte[] bytes = Convert.FromHexString(hex);
            var result = FontMetadataReader.Inspect(bytes);
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Unsupported));
            Assert.That(result.Metadata!.Format, Is.EqualTo(format));
            Assert.That(FontMetadataReader.IsFormatSupported(format), Is.False);
            var exception = Assert.Throws<FontFeatureNotSupportedException>(() => FontMetadataReader.Read(bytes))!;
            Assert.That(exception.Result.Metadata!.Format, Is.EqualTo(format));
            Assert.That(FontMetadataReader.HasFontExtension("FONT.WOFF2"), Is.True);
        }
        [Test]
        public void UnknownAndEmptyInputsAreNotRecognized() {
            foreach(var bytes in new[] { Array.Empty<byte>(), [1, 2, 3, 4] }) {
                var result = FontMetadataReader.Inspect(bytes);
                Assert.That(result.Status, Is.EqualTo(FontReadStatus.NotRecognized));
                Assert.That(result.Metadata, Is.Null);
                Assert.Throws<FontReadException>(() => FontMetadataReader.Read(bytes));
            }
        }
        [Test]
        public void EveryTruncationAndDeterministicMutationsProduceStructuredResults() {
            byte[] font = Collection([("name", Names(Name.Text(FamilyNameId, "Example"))), ("OS/2", Os2())], [("name", Names(Name.Text(LicenseDescriptionNameId, "License")))]);
            for(int length = 0; length < font.Length; length++) {
                var result = FontMetadataReader.Inspect(font.AsSpan(0, length));
                Assert.That(Enum.IsDefined(result.Status), Is.True);
                if(result.Status is FontReadStatus.Failed or FontReadStatus.Partial or FontReadStatus.Unsupported)
                    Assert.That(result.Diagnostics, Is.Not.Empty);
            }
            const int mutationSeed = 90427, mutationCount = 1000, changesPerInput = 4;
            var random = new Random(mutationSeed);
            for(int iteration = 0; iteration < mutationCount; iteration++) {
                byte[] mutated = (byte[])font.Clone();
                for(int i = 0; i < changesPerInput; i++)
                    mutated[random.Next(mutated.Length)] = (byte)random.Next(byte.MaxValue + 1);
                var result = FontMetadataReader.Inspect(mutated, new FontReadOptions { Limits = new FontReadLimits { MaxDiagnostics = 8, MaxNameRecords = 32 } });
                Assert.That(result.Diagnostics.Count, Is.LessThanOrEqualTo(8));
            }
        }
        sealed class ControlledStream : Stream {
            const int ShortReadSize = 3;
            const string InjectedFailureMessage = "Injected read failure.";
            readonly MemoryStream inner;
            readonly bool seekable, throwIo;
            readonly Action? afterRead;
            internal ControlledStream(byte[] data, bool seekable = true, bool throwIo = false, Action? afterRead = null) {
                inner = new MemoryStream(data);
                this.seekable = seekable;
                this.throwIo = throwIo;
                this.afterRead = afterRead;
            }
            internal bool Disposed { get; private set; }
            internal int BytesRead { get; private set; }
            public override bool CanRead { get { return !Disposed; } }
            public override bool CanSeek { get { return seekable; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { return seekable ? inner.Length : throw new NotSupportedException(); } }
            public override long Position {
                get { return seekable ? inner.Position : throw new NotSupportedException(); }
                set {
                    if(!seekable)
                        throw new NotSupportedException();
                    inner.Position = value;
                }
            }
            public override int Read(byte[] buffer, int offset, int count) {
                if(throwIo) {
                    inner.Position++;
                    throw new IOException(InjectedFailureMessage);
                }
                int result = inner.Read(buffer, offset, Math.Min(count, ShortReadSize));
                BytesRead += result;
                afterRead?.Invoke();
                return result;
            }
            protected override void Dispose(bool disposing) {
                Disposed = true;
                if(disposing)
                    inner.Dispose();
                base.Dispose(disposing);
            }
            public override void Flush() {
                throw new NotSupportedException();
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
