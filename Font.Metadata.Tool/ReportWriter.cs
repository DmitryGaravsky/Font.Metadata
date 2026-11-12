using System;
using System.Collections.Generic;
using System.Threading;

namespace Fonts.Metadata.Tool {
    internal abstract class ReportWriter : IDisposable {
        protected ReportWriter(CommandOptions options, CancellationToken token) {
            Options = options;
            Token = token;
        }
        protected CommandOptions Options { get; }
        protected CancellationToken Token { get; }
        internal abstract void Begin();
        internal abstract void Write(SourceResult result);
        internal abstract void End();
        public abstract void Dispose();
    }

    // Explicit wire vocabulary: adding a library enum never silently changes the CLI schema.
    internal static class ReportValues {
        internal static string Status(FontReadStatus value) {
            return value switch {
                FontReadStatus.Complete => "complete",
                FontReadStatus.Partial => "partial",
                FontReadStatus.Failed => "failed",
                FontReadStatus.Unsupported => "unsupported",
                FontReadStatus.NotRecognized => "notRecognized",
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
        }
        internal static string Section(FontSectionStatus value) {
            return value switch {
                FontSectionStatus.NotRead => "notRead",
                FontSectionStatus.Missing => "missing",
                FontSectionStatus.Complete => "complete",
                FontSectionStatus.Partial => "partial",
                FontSectionStatus.Invalid => "invalid",
                FontSectionStatus.Unsupported => "unsupported",
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
        }
        internal static string Format(FontFormat value) {
            return value switch {
                FontFormat.Unknown => "unknown",
                FontFormat.Sfnt => "sfnt",
                FontFormat.Collection => "collection",
                FontFormat.Woff => "woff",
                FontFormat.Woff2 => "woff2",
                FontFormat.Eot => "eot",
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
        }
        internal static string Source(FontFaceSource value) {
            return value switch {
                FontFaceSource.Container => "container",
                FontFaceSource.EotMain => "eotMain",
                FontFaceSource.EotEudc => "eotEudc",
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
        }
        internal static string Severity(FontDiagnosticSeverity value) {
            return value switch {
                FontDiagnosticSeverity.Warning => "warning",
                FontDiagnosticSeverity.Error => "error",
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
        }
        internal static string Category(FontDiagnosticCategory value) {
            return value switch {
                FontDiagnosticCategory.MalformedData => "malformedData",
                FontDiagnosticCategory.LimitExceeded => "limitExceeded",
                FontDiagnosticCategory.UnsupportedFeature => "unsupportedFeature",
                FontDiagnosticCategory.UnsupportedEncoding => "unsupportedEncoding",
                FontDiagnosticCategory.NonConformantData => "nonConformantData",
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
        }
        internal static string IdentifierKind(FontLicenseIdentifierKind value) {
            return value switch {
                FontLicenseIdentifierKind.Spdx => "spdx",
                FontLicenseIdentifierKind.Family => "family",
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
        }
        internal static IEnumerable<(string Key, string Label, string? Value)> Preferred(FontMetadata.Face face) {
            yield return ("familyName", "Family name", face.FamilyName);
            yield return ("subfamilyName", "Subfamily name", face.SubfamilyName);
            yield return ("fullName", "Full name", face.FullName);
            yield return ("postScriptName", "PostScript name", face.PostScriptName);
            yield return ("version", "Version", face.Version);
            yield return ("copyright", "Copyright", face.Copyright);
            yield return ("trademark", "Trademark", face.Trademark);
            yield return ("manufacturer", "Manufacturer", face.Manufacturer);
            yield return ("designer", "Designer", face.Designer);
            yield return ("description", "Description", face.Description);
            yield return ("vendorUrl", "Vendor URL", face.VendorUrl);
            yield return ("designerUrl", "Designer URL", face.DesignerUrl);
            yield return ("licenseDescription", "License description", face.LicenseDescription);
            yield return ("licenseUrl", "License URL", face.LicenseUrl);
        }
    }
}
