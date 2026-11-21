using System.Collections.Generic;

namespace Fonts.Metadata.Tool {
    internal sealed partial class JsonReportWriter {
        void WriteUnicodeCoverage(FontUnicodeCoverage coverage) {
            writer.WriteStartObject("unicodeCoverage");
            writer.WriteString("status", ReportValues.Section(coverage.Status));
            Number("platformId", coverage.PlatformId);
            Number("encodingId", coverage.EncodingId);
            Number("format", coverage.Format);
            writer.WriteStartArray("ranges");
            foreach(var range in coverage.Ranges) {
                Checkpoint();
                writer.WriteStartObject();
                writer.WriteNumber("start", range.Start);
                writer.WriteNumber("end", range.End);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            WriteSectionDiagnostics(coverage.Diagnostics);
            writer.WriteEndObject();
        }
        void WriteVariationAxes(FontVariationAxes variations) {
            writer.WriteStartObject("variationAxes");
            writer.WriteString("status", ReportValues.Section(variations.Status));
            writer.WriteStartArray("axes");
            foreach(var axis in variations.Axes) {
                Checkpoint();
                writer.WriteStartObject();
                writer.WriteNumber("index", axis.Index);
                writer.WriteString("tag", axis.Tag);
                writer.WriteNumber("minimum", axis.Minimum);
                writer.WriteNumber("default", axis.Default);
                writer.WriteNumber("maximum", axis.Maximum);
                writer.WriteNumber("flags", axis.Flags);
                writer.WriteNumber("nameId", axis.NameId);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            WriteSectionDiagnostics(variations.Diagnostics);
            writer.WriteEndObject();
        }
        void WriteSectionDiagnostics(IReadOnlyList<FontDiagnostic> diagnostics) {
            writer.WriteStartArray("diagnostics");
            foreach(var diagnostic in diagnostics)
                WriteDiagnostic(diagnostic);
            writer.WriteEndArray();
        }
    }
}
