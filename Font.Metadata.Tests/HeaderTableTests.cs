using System;
using System.Buffers.Binary;
using System.Linq;
using System.Threading;
using Fonts.Metadata.Internal;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;
using static Fonts.Metadata.Tests.WebFontFixtures;

namespace Fonts.Metadata.Tests {
    [TestFixture]
    public sealed class HeaderTableTests {
        static byte[] Head(uint revision = 0x000300c5) {
            byte[] data = new byte[54];
            U32(data, 0, 0x00010000);
            U32(data, 4, revision);
            U32(data, 12, 0x5f0f3cf5);
            U16(data, 16, 15);
            U16(data, 18, 2048);
            BinaryPrimitives.WriteInt64BigEndian(data.AsSpan(20), long.MinValue);
            BinaryPrimitives.WriteInt64BigEndian(data.AsSpan(28), long.MaxValue);
            U16(data, 44, 3);
            return data;
        }
        static byte[] Maxp(bool cff, ushort glyphs) {
            byte[] data = new byte[cff ? 6 : 32];
            U32(data, 0, cff ? 0x5000u : 0x10000u);
            U16(data, 4, glyphs);
            return data;
        }
        [TestCase("sfnt")]
        [TestCase("collection")]
        [TestCase("woff")]
        [TestCase("woff2")]
        [TestCase("woff2-collection")]
        [TestCase("eot-xor")]
        public void ReadsFieldsAndDirectoryForEveryFace(string format) {
            var head = Head();
            var maxp = Maxp(false, 1150);
            (string, byte[])[] fields = [("cvt ", new byte[2]), ("head", head), ("maxp", maxp)];
            Table[] tables = [new("cvt ", new byte[2]), new("head", head, true), new("maxp", maxp, true)];
            byte[] data = format switch {
                "sfnt" => Sfnt(fields),
                "collection" => Collection(fields, fields),
                "woff" => Woff(tables),
                "woff2" => Woff2(tables),
                "woff2-collection" => Woff2(tables, [[0, 1, 2], [0, 1, 2]]),
                _ => Eot(Sfnt(fields), flags: 0x10000000, eudc: Sfnt(fields))
            };
            var result = FontMetadataReader.Inspect(data, new FontReadOptions { IncludeTableInventory = true });
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete));
            Assert.That(result.Faces.Count, Is.EqualTo(format is "collection" or "woff2-collection" or "eot-xor" ? 2 : 1));
            foreach(var face in result.Faces) {
                Assert.That(face.HeadTableStatus, Is.EqualTo(FontSectionStatus.Complete));
                Assert.That(face.Head!.FontRevision, Is.EqualTo(196805));
                Assert.That(face.Head.Created, Is.EqualTo(long.MinValue));
                Assert.That(face.Head.Modified, Is.EqualTo(long.MaxValue));
                Assert.That((face.Head.Flags, face.Head.UnitsPerEm, face.Head.MacStyle), Is.EqualTo((15, 2048, 3)));
                Assert.That(face.MaxpTableStatus, Is.EqualTo(FontSectionStatus.Complete));
                Assert.That(face.GlyphCount, Is.EqualTo(1150));
                var inventory = face.TableInventory!;
                Assert.That(inventory.Status, Is.EqualTo(FontSectionStatus.Complete));
                Assert.That(inventory.Entries.Select(t => t.Tag), Is.EqualTo(new[] { "cvt ", "head", "maxp" }));
                var declaration = inventory.Entries[1];
                Assert.That(declaration.Length, Is.EqualTo(54));
                Assert.That(declaration.IsCompressed, Is.EqualTo(format == "woff"));
                Assert.That(declaration.StoredLength, format == "woff" ? Is.LessThan(54) : Is.EqualTo(54));
            }
        }
        [Test]
        public void UnsupportedWoff2TransformPreservesCompleteInventory() {
            byte[] input = Woff2([new Table("TEST", new byte[4], Transform: 1, Original: 8)]);
            var result = FontMetadataReader.Inspect(input, new FontReadOptions { IncludeTableInventory = true });
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Unsupported));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.UnsupportedTransform));
            var inventory = result.Faces.Single().TableInventory!;
            Assert.That(inventory.Status, Is.EqualTo(FontSectionStatus.Complete));
            var entry = inventory.Entries.Single();
            Assert.That((entry.Tag, entry.Length, entry.StoredLength), Is.EqualTo(("TEST", 8L, 4L)));
            Assert.That(entry.IsTransformed, Is.True);
            Assert.That(entry.TransformVersion, Is.EqualTo(1));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void AmbiguousOrUnresolvedWoff2ReferenceLeavesIncompleteInventory(bool duplicate) {
            Table[] tables = [new("TEST", new byte[4], Transform: 1, Original: 8), new("prep", new byte[4])];
            // The final reference either duplicates prep or points past the global dictionary.
            ushort[] references = duplicate ? [0, 1, 1] : [0, 2];
            byte[] input = Woff2(tables, [references, [1]]);
            var result = FontMetadataReader.Inspect(input, new FontReadOptions { IncludeTableInventory = true });
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(FontDiagnosticCodes.InvalidDirectory));
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(FontDiagnosticCodes.UnsupportedTransform));
            var inventory = result.Faces[0].TableInventory!;
            Assert.That(inventory.Status, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(inventory.Entries.Single().Tag, Is.EqualTo("TEST"));
            Assert.That(result.Faces[1].TableInventory!.Status, Is.EqualTo(FontSectionStatus.Complete));
        }
        [TestCase(0u, 0)]
        [TestCase(0xffffffffu, -1)]
        public void PreservesZeroAndSignedRevision(uint raw, int expected) {
            var face = FontMetadataReader.Inspect(Sfnt(("head", Head(raw)), ("maxp", Maxp(true, 0)))).Faces.Single();
            Assert.That(face.Head!.FontRevision, Is.EqualTo(expected));
            Assert.That(face.GlyphCount, Is.Zero);
            Assert.That(face.TableInventory, Is.Null);
        }
        [TestCase("head", "length")]
        [TestCase("head", "version")]
        [TestCase("head", "magic")]
        [TestCase("maxp", "length")]
        [TestCase("maxp", "version")]
        public void RejectsOnlyDamagedTable(string tag, string mutation) {
            var table = tag == "head" ? Head() : Maxp(false, 25);
            if(mutation == "length")
                Array.Resize(ref table, table.Length - 1);
            else if(mutation == "magic")
                table[12] = 0;
            else
                table[3] = 1;
            var result = FontMetadataReader.Inspect(Sfnt(("name", NameData()), (tag, table)));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            var face = result.Faces.Single();
            Assert.That(face.FamilyName, Is.EqualTo("Example Sans"));
            Assert.That(tag == "head" ? face.HeadTableStatus : face.MaxpTableStatus,
                Is.EqualTo(mutation == "version" ? FontSectionStatus.Unsupported : FontSectionStatus.Invalid));
            Assert.That(face.Head, Is.Null);
            Assert.That(face.GlyphCount, Is.Null);
            var error = result.Diagnostics.Single(d => d.Severity == FontDiagnosticSeverity.Error);
            Assert.That(error.TableTag, Is.EqualTo(tag));
            Assert.That(error.ByteOffset, Is.Not.Null);
            Assert.That(error.TableByteOffset, Is.Not.Null);
        }
        [TestCase("head", false)]
        [TestCase("head", true)]
        [TestCase("maxp", false)]
        [TestCase("maxp", true)]
        public void ReadableHeadersPreserveLegacyFailureAndStrictException(string tag, bool unsupported) {
            byte[] name = unsupported ? [0, 7, 0, 0, 0, 6] : [];
            byte[] data = Sfnt(("name", name), (tag, tag == "head" ? Head() : Maxp(true, 7)));
            var result = FontMetadataReader.Inspect(data);
            Assert.That(result.Status, Is.EqualTo(unsupported ? FontReadStatus.Unsupported : FontReadStatus.Failed));
            var face = result.Faces.Single();
            if(tag == "head")
                Assert.That(face.Head!.FontRevision, Is.EqualTo(196805));
            else
                Assert.That(face.GlyphCount, Is.EqualTo(7));
            if(unsupported)
                Assert.Throws<FontFeatureNotSupportedException>(() => FontMetadataReader.Read(data));
            else
                Assert.Throws<FontReadException>(() => FontMetadataReader.Read(data));
        }
        [TestCase("head")]
        [TestCase("maxp")]
        public void LoneInvalidHeaderRequiresIndependentMetadataForPartialRecovery(string tag) {
            var empty = FontMetadataReader.Inspect(Sfnt(("prep", new byte[4])));
            Assert.That(empty.Status, Is.EqualTo(FontReadStatus.Complete));
            var invalid = FontMetadataReader.Inspect(Sfnt((tag, new byte[1])));
            Assert.That(invalid.Status, Is.EqualTo(FontReadStatus.Failed));
            Assert.That(invalid.Faces.Single().Head, Is.Null);
            Assert.That(invalid.Faces.Single().GlyphCount, Is.Null);
            var sibling = tag == "head" ? ("maxp", Maxp(true, 7)) : ("head", Head());
            var recovered = FontMetadataReader.Inspect(Sfnt((tag, new byte[1]), sibling));
            Assert.That(recovered.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(recovered.Diagnostics.Single().TableTag, Is.EqualTo(tag));
        }
        [TestCase("reserved")]
        [TestCase("padding")]
        [TestCase("optional")]
        [TestCase("overlap")]
        public void WoffInventoryTracksReadableDirectoryRecords(string error) {
            byte[] data = Woff(new Table("head", Head()), new Table("maxp", Maxp(true, 7)), new Table("name", NameData()));
            int headStart = ReadOffset(data, 48);
            switch(error) {
                case "reserved":
                    U16(data, 14, 1);
                    break;
                case "padding":
                    data[headStart + 54] = 1;
                    break;
                case "optional":
                    U32(data, 32, 4);
                    break;
                case "overlap":
                    U32(data, 68, (uint)headStart);
                    break;
            }
            var result = FontMetadataReader.Inspect(data, new FontReadOptions { IncludeTableInventory = true });
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            var inventory = result.Faces.Single().TableInventory!;
            bool overlap = error == "overlap";
            Assert.That(inventory.Status, Is.EqualTo(overlap ? FontSectionStatus.Partial : FontSectionStatus.Complete));
            Assert.That(inventory.Entries.Select(t => t.Tag), Is.EqualTo(overlap ? new[] { "name" } : new[] { "head", "maxp", "name" }));
        }
        [Test]
        public void CompletedWoff2HeadersSurviveAnIncompleteStream() {
            byte[] data = Woff2([new Table("head", Head()), new Table("maxp", Maxp(true, 7))]);
            int length = ReadOffset(data, 20);
            Array.Resize(ref data, data.Length + 1);
            U32(data, 8, (uint)data.Length);
            U32(data, 20, (uint)(length + 1));
            var result = FontMetadataReader.Inspect(data, new FontReadOptions { IncludeTableInventory = true });
            var face = result.Faces.Single();
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(face.HeadTableStatus, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(face.MaxpTableStatus, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(face.Head!.FontRevision, Is.EqualTo(196805));
            Assert.That(face.GlyphCount, Is.EqualTo(7));
            Assert.That(face.TableInventory!.Status, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(face.TableInventory.Entries.Select(t => t.Tag), Is.EqualTo(new[] { "head", "maxp" }));
        }
        [Test]
        public void CompletedCtfHeadersSurviveATruncatedThirdMtxStream() {
            byte[] data = EotMtxFixtures.Create(false);
            var options = new FontReadOptions { IncludeTableInventory = true };
            var intact = FontMetadataReader.Read(data, options).Faces.Single();
            int payloadSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4));
            int start = data.Length - payloadSize;
            int third = data[start + 7] << 16 | data[start + 8] << 8 | data[start + 9];
            Array.Resize(ref data, start + third + 1);
            BinaryPrimitives.WriteInt32LittleEndian(data, data.Length);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), data.Length - start);
            var result = FontMetadataReader.Inspect(data, options);
            var face = result.Faces.Single();
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Partial));
            Assert.That(face.HeadTableStatus, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(face.MaxpTableStatus, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(face.Head!.FontRevision, Is.EqualTo(intact.Head!.FontRevision));
            Assert.That(face.GlyphCount, Is.EqualTo(3));
            Assert.That(face.TableInventory!.Status, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(face.TableInventory.Entries.Select(t => (t.Tag, t.Length)),
                Is.EqualTo(intact.TableInventory!.Entries.Select(t => (t.Tag, t.Length))));
        }
        [Test]
        public void InventoryRequiresNoAdditionalDecodeBudget() {
            byte[] data = Woff(new Table("cvt ", new byte[1024], true), new Table("head", Head(), true));
            foreach(bool inventory in new[] { false, true }) {
                var result = FontMetadataReader.Inspect(data, new FontReadOptions {
                    IncludeTableInventory = inventory,
                    Limits = new FontReadLimits { MaxTotalDecodedBytes = 54 }
                });
                Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete));
                Assert.That(result.Faces.Single().MaxpTableStatus, Is.EqualTo(FontSectionStatus.Missing));
            }
        }
        [Test]
        public void TransformedLocaRetainsItsZeroLengthRepresentation() {
            var result = FontMetadataReader.Inspect(Woff2([new Table("glyf", [0], Original: 12),
                new Table("loca", [], Original: 8)]), new FontReadOptions { IncludeTableInventory = true });
            var loca = result.Faces.Single().TableInventory!.Entries.Single(t => t.Tag == "loca");
            Assert.That(loca.Length, Is.EqualTo(8));
            Assert.That(loca.StoredLength, Is.Zero);
            Assert.That(loca.TransformVersion, Is.Zero);
            Assert.That(loca.IsTransformed, Is.True);
        }
        [Test]
        public void CanCancelDuringInventoryTraversal() {
            byte[] data = Sfnt(("cvt ", new byte[2]), ("head", Head()), ("maxp", Maxp(true, 3)));
            int baselineChecks = 0;
            var baseline = new ReadingContext(new FontReadLimits(), CancellationToken.None, () => baselineChecks++);
            new SfntParser(data, baseline, new SourceLocation(0, 0)).ReadSingleFace(0);
            using(var cancellation = new CancellationTokenSource()) {
                int checks = 0;
                void Check() {
                    if(++checks > baselineChecks)
                        cancellation.Cancel();
                    cancellation.Token.ThrowIfCancellationRequested();
                }
                var context = new ReadingContext(new FontReadLimits(), cancellation.Token, Check) { IncludeTableInventory = true };
                var error = Assert.Throws<OperationCanceledException>(() =>
                    new SfntParser(data, context, new SourceLocation(0, 0)).ReadSingleFace(0));
                Assert.That(error!.CancellationToken, Is.EqualTo(cancellation.Token));
            }
        }
        [Test]
        public void SharedTtcHeadIsReadOnceAndListedPerFace() {
            byte[] input = Collection([("head", Head())], [("head", Head())]);
            int first = ReadOffset(input, 12);
            int second = ReadOffset(input, 16);
            // Absolute TTC table offsets follow the 12-byte SFNT header and the tag/checksum pair.
            U32(input, second + 20, (uint)ReadOffset(input, first + 20));
            var result = FontMetadataReader.Inspect(input, new FontReadOptions { IncludeTableInventory = true });
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete));
            Assert.That(result.Faces[1].Head, Is.SameAs(result.Faces[0].Head));
            Assert.That(result.Faces.Select(f => f.TableInventory!.Entries.Single().Tag), Is.EqualTo(new[] { "head", "head" }));
        }
        [Test]
        public void HeaderTableLimitKeepsMaxpAndInventory() {
            byte[] input = Sfnt(("head", Head()), ("maxp", Maxp(true, 0)));
            var result = FontMetadataReader.Inspect(input, new FontReadOptions {
                IncludeTableInventory = true,
                Limits = new FontReadLimits { MaxTableBytes = 32 }
            });
            var face = result.Faces.Single();
            Assert.That(face.HeadTableStatus, Is.EqualTo(FontSectionStatus.NotRead));
            Assert.That(face.GlyphCount, Is.Zero);
            Assert.That(face.TableInventory!.Status, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(result.Diagnostics.Single().LimitReason, Is.EqualTo("MaxTableBytes"));
        }
        [Test]
        public void DuplicateEntriesDoNotClaimAnUnambiguousTable() {
            var data = Sfnt(("cvt ", new byte[4]), ("head", Head()), ("head", Head()));
            var result = FontMetadataReader.Inspect(data, new FontReadOptions { IncludeTableInventory = true });
            var face = result.Faces.Single();
            Assert.That(face.Head, Is.Null);
            Assert.That(face.TableInventory!.Status, Is.EqualTo(FontSectionStatus.Partial));
            Assert.That(face.TableInventory.Entries.Select(entry => entry.Tag), Is.EqualTo(new[] { "cvt " }));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void ReadsDirectoryAndMetricsFromMtx(bool runLength) {
            var result = FontMetadataReader.Inspect(EotMtxFixtures.Create(runLength), new FontReadOptions { IncludeTableInventory = true });
            var face = result.Faces.Single();
            Assert.That(face.HeadTableStatus, Is.EqualTo(FontSectionStatus.Complete));
            Assert.That(face.GlyphCount, Is.EqualTo(3));
            Assert.That(face.TableInventory!.Entries.Select(t => t.Tag), Does.Contain("maxp"));
        }
        [Test]
        public void PreservesNonconformantUnitsAndAttributesDiagnosticToHead() {
            byte[] head = Head();
            U16(head, 18, 1);
            var result = FontMetadataReader.Inspect(Sfnt(("head", head)));
            Assert.That(result.Status, Is.EqualTo(FontReadStatus.Complete));
            Assert.That(result.Faces.Single().Head!.UnitsPerEm, Is.EqualTo(1));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(FontDiagnosticCodes.NonConformantHead));
        }
        [Test]
        public void ExpansionAndTotalBudgetsAreDistinguishable() {
            byte[] data = Woff2([new Table("head", Head()), new Table("junk", new byte[2000])]);
            foreach(bool ratio in new[] { true, false }) {
                var limits = ratio ? new FontReadLimits { MaxExpansionRatio = 1 } : new FontReadLimits { MaxTotalDecodedBytes = 1 };
                var result = FontMetadataReader.Inspect(data, new FontReadOptions { Limits = limits });
                Assert.That(result.Diagnostics.Single(d => d.Code == FontDiagnosticCodes.LimitExceeded).LimitReason,
                    Is.EqualTo(ratio ? "MaxExpansionRatio" : "MaxTotalDecodedBytes"));
            }
        }
    }
}
