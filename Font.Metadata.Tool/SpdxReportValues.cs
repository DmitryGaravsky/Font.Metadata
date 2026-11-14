using System;
using Spdx.Expressions;

namespace Fonts.Metadata.Tool {
    internal static class SpdxReportValues {
        internal static string Status(SpdxParseStatus value) {
            return value switch {
                SpdxParseStatus.Success => "success",
                SpdxParseStatus.InvalidSyntax => "invalidSyntax",
                SpdxParseStatus.LimitExceeded => "limitExceeded",
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
        }
        internal static string Severity(SpdxDiagnosticSeverity value) {
            return value switch {
                SpdxDiagnosticSeverity.Warning => "warning",
                SpdxDiagnosticSeverity.Error => "error",
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
        }
        internal static string Role(SpdxIdentifierRole value) {
            return value switch {
                SpdxIdentifierRole.License => "license",
                SpdxIdentifierRole.Exception => "exception",
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
        }
        internal static string Lookup(SpdxIdentifierLookupStatus value) {
            return value switch {
                SpdxIdentifierLookupStatus.Known => "known",
                SpdxIdentifierLookupStatus.Unknown => "unknown",
                SpdxIdentifierLookupStatus.WrongKind => "wrongKind",
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
        }
    }
}
