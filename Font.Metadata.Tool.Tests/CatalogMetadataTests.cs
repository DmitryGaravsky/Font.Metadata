using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using static Fonts.Metadata.Tests.FontFixtures;
using static Fonts.Metadata.Tests.WebFontFixtures;

namespace Fonts.Metadata.Tool.Tests {
    [TestFixture]
    public sealed class CatalogMetadataTests {
        // A Windows UCS-4 subtable maps U+1F600..U+1F601 to glyphs 1..2.
        static byte[] Coverage() {
            return Convert.FromHexString("000000010003000A0000000C000C00000000001C00000000000000010001F6000001F60100000001");
        }
        // One hidden wght axis, 0.5 .. 1.5 with default 1 and name ID 256.
        static byte[] Axes() {
            return Convert.FromHexString("000100000010000200010014000000087767687400008000000100000001800000010100");
        }
        [TestCase("sfnt")]
        [TestCase("ttc")]
        [TestCase("woff")]
        [TestCase("woff2")]
        [TestCase("woff2-collection")]
        [TestCase("eot")]
        public async Task BothSectionsFollowFaceThroughEachContainer(string kind) {
            var tables = new[] { ("cmap", Coverage()), ("fvar", Axes()), ("name", NameData()) };
            var web = tables.Select(t => new Table(t.Item1, t.Item2)).ToArray();
            byte[] input = kind switch {
                "ttc" => Collection(tables, tables),
                "woff" => Woff(web),
                "woff2" => Woff2(web),
                "woff2-collection" => Woff2(web, [[0, 1, 2], [0, 1, 2]]),
                "eot" => Eot(Sfnt(tables), eudc: Sfnt(tables)),
                _ => Sfnt(tables)
            };
            var result = await ToolProcess.Run(["inspect", "-", "--format", "json", "--include-variation-axes", "--include-unicode-coverage"], input);
            Assert.That(result.Code, Is.Zero, result.Error);
            var faces = result.Json().GetProperty("results")[0].GetProperty("metadata").GetProperty("faces");
            Assert.That(faces.GetArrayLength(), Is.EqualTo(kind is "ttc" or "woff2-collection" or "eot" ? 2 : 1));
            foreach(var face in faces.EnumerateArray()) {
                var coverage = face.GetProperty("unicodeCoverage");
                var axes = face.GetProperty("variationAxes");
                Assert.That(coverage.GetProperty("status").GetString(), Is.EqualTo("complete"));
                Assert.That(coverage.GetProperty("ranges").GetArrayLength(), Is.EqualTo(1));
                Assert.That(coverage.GetProperty("ranges")[0].GetProperty("start").GetInt32(), Is.EqualTo(0x1F600));
                Assert.That(coverage.GetProperty("ranges")[0].GetProperty("end").GetInt32(), Is.EqualTo(0x1F601));
                Assert.That(axes.GetProperty("status").GetString(), Is.EqualTo("complete"));
                var axis = axes.GetProperty("axes")[0];
                Assert.That(axis.GetProperty("tag").GetString(), Is.EqualTo("wght"));
                Assert.That(axis.GetProperty("minimum").GetDecimal(), Is.EqualTo(0.5m));
                Assert.That(axis.GetProperty("maximum").GetDecimal(), Is.EqualTo(1.5m));
                Assert.That(axis.GetProperty("nameId").GetInt32(), Is.EqualTo(256));
            }
        }
        [Test]
        public async Task CompleteCollectionReportsUnsupportedSymbolCoveragePerFace() {
            // A Windows symbol encoding record points to a format 4 subtable mapping U+F020 to glyph 1.
            var symbol = Convert.FromHexString("00000001000300000000000C0004002000000004000400010000F020FFFF0000F020FFFF0FE1000100000000");
            var font = Collection([("cmap", symbol), ("name", NameData())], [("name", NameData())]);
            var result = await ToolProcess.Run(["inspect", "-", "--format", "json", "--include-unicode-coverage"], font);
            Assert.That(result.Code, Is.Zero, result.Error);
            var row = result.Json().GetProperty("results")[0];
            Assert.That(row.GetProperty("status").GetString(), Is.EqualTo("complete"));
            var faces = row.GetProperty("metadata").GetProperty("faces");
            var coverage = faces[0].GetProperty("unicodeCoverage");
            Assert.That(coverage.GetProperty("status").GetString(), Is.EqualTo("unsupported"));
            Assert.That(coverage.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("FM_UNSUPPORTED_CMAP_SUBTABLE"));
            Assert.That(faces[1].GetProperty("unicodeCoverage").GetProperty("status").GetString(), Is.EqualTo("missing"));
        }
        [TestCase(false)]
        [TestCase(true)]
        public async Task AutomaticAndAuditedAxesFollowEachFaceDirectory(bool audit) {
            byte[] data = Collection(
                [("fvar", Axes()), ("name", NameData())],
                [("name", NameData())],
                [("fvar", new byte[1])],
                [("name", NameData())]);
            U32(data, CollectionFacePosition(3), uint.MaxValue);
            string[] flags = audit ? ["--include-variation-axes"] : [];
            var result = await ToolProcess.Run(["inspect", "-", "--format", "json", .. flags], data);
            Assert.That(result.Code, Is.EqualTo(1));
            var report = result.Json();
            Assert.That(report.GetProperty("options").TryGetProperty("includeVariationAxes", out _), Is.EqualTo(audit));
            var faces = report.GetProperty("results")[0].GetProperty("metadata").GetProperty("faces");
            Assert.That(faces[0].GetProperty("variationAxes").GetProperty("axes")[0].GetProperty("tag").GetString(), Is.EqualTo("wght"));
            Assert.That(faces[2].GetProperty("variationAxes").GetProperty("status").GetString(), Is.EqualTo("invalid"));
            Assert.That(faces[1].TryGetProperty("variationAxes", out var missing), Is.EqualTo(audit));
            Assert.That(faces[3].TryGetProperty("variationAxes", out var unknown), Is.EqualTo(audit));
            if(audit) {
                Assert.That(missing.GetProperty("status").GetString(), Is.EqualTo("missing"));
                Assert.That(unknown.GetProperty("status").GetString(), Is.EqualTo("notRead"));
            }
            foreach(var face in faces.EnumerateArray())
                Assert.That(face.TryGetProperty("unicodeCoverage", out _), Is.False);
            var plain = await ToolProcess.Run(["inspect", "-", .. flags], data);
            Assert.That(plain.Code, Is.EqualTo(result.Code));
            Assert.That(plain.Output.Split("Variation axes:").Length - 1, Is.EqualTo(audit ? 4 : 2));
            Assert.That(plain.Output, Does.Contain("wght"));
            Assert.That(plain.Output, Does.Not.Contain("Unicode coverage:"));
        }
        [Test]
        public async Task PresentUnsupportedTableIsShownButUnavailablePayloadIsHidden() {
            byte[] table = Axes();
            U16(table, 0, 2);
            byte[] data = Eot(Sfnt(("fvar", table)), eudc: Sfnt(("name", NameData())), eudcFlags: 0x80000000);
            foreach(bool audit in new[] { false, true }) {
                string[] flags = audit ? ["--include-variation-axes"] : [];
                var result = await ToolProcess.Run(["inspect", "-", "--format", "json", .. flags], data);
                Assert.That(result.Code, Is.EqualTo(1));
                var faces = result.Json().GetProperty("results")[0].GetProperty("metadata").GetProperty("faces");
                Assert.That(faces[0].GetProperty("variationAxes").GetProperty("status").GetString(), Is.EqualTo("unsupported"));
                Assert.That(faces[1].TryGetProperty("variationAxes", out var unknown), Is.EqualTo(audit));
                if(audit)
                    Assert.That(unknown.GetProperty("status").GetString(), Is.EqualTo("unsupported"));
            }
        }
        [TestCase("--include-unicode-coverage", "unicodeCoverage", "variationAxes", "invalid")]
        [TestCase("--include-variation-axes", "variationAxes", "unicodeCoverage", "missing")]
        public async Task SectionStatusAndOptionPresenceAreIndependent(string flag, string enabled, string disabled, string status) {
            byte[] data = Sfnt(("cmap", new byte[1]), ("name", NameData()));
            var result = await ToolProcess.Run(["inspect", "-", "--format", "json", flag], data);
            Assert.That(result.Code, Is.Zero);
            JsonElement row = result.Json().GetProperty("results")[0];
            var face = row.GetProperty("metadata").GetProperty("faces")[0];
            Assert.That(row.GetProperty("status").GetString(), Is.EqualTo("complete"));
            Assert.That(face.GetProperty("familyName").GetString(), Is.EqualTo("Example Sans"));
            Assert.That(face.TryGetProperty(disabled, out _), Is.False);
            Assert.That(face.GetProperty(enabled).GetProperty("status").GetString(), Is.EqualTo(status));
        }
    }
}
