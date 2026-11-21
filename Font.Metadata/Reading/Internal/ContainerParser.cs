using System;
using System.Threading;

namespace Fonts.Metadata.Internal {
    internal static class ContainerParser {
        internal static FontInspectionResult Parse(ReadOnlySpan<byte> data, FontReadOptions options, CancellationToken token) {
            var context = new ReadingContext(options.Limits, token) {
                IncludeUnicodeCoverage = options.IncludeUnicodeCoverage,
                IncludeVariationAxes = options.IncludeVariationAxes
            };
            token.ThrowIfCancellationRequested();
            var format = SfntParser.Recognize(data);
            if(format == FontFormat.Unknown) {
                var emptyDiagnostics = context.Diagnostics.Freeze();
                return new FontInspectionResult(FontReadStatus.NotRecognized, null, emptyDiagnostics);
            }
            switch(format) {
                case FontFormat.Sfnt:
                case FontFormat.Collection:
                    var sfnt = new SfntParser(data, context, new SourceLocation(0, 0));
                    sfnt.Read(format);
                    break;
                case FontFormat.Woff:
                    WoffParser.Read(data, context);
                    break;
                case FontFormat.Woff2:
                    Woff2Parser.Read(data, context);
                    break;
                case FontFormat.Eot:
                    EotParser.Read(data, context);
                    break;
            }
            token.ThrowIfCancellationRequested();
            context.FinishSectionDiagnostics();
            var status = context.GetStatus();
            var faceSnapshot = ResultList<FontMetadata.Face>.Snapshot(context.Faces);
            var eotStatus = format == FontFormat.Eot ? context.EotStatus : FontSectionStatus.Missing;
            var metadata = new FontMetadata(format, faceSnapshot, context.Eot, eotStatus);
            var diagnostics = context.Diagnostics.Freeze();
            return new FontInspectionResult(status, metadata, diagnostics);
        }
    }
}
