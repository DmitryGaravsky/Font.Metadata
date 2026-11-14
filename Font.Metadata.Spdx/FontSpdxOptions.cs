using System;
using Spdx.Expressions;

namespace Fonts.Metadata {
    /// <summary>Inclusive per-face work and result limits; not a process memory or time ceiling.</summary>
    public sealed class FontSpdxOptions {
        /// <summary>Maximum visited records, including non-license records. Default 4096.</summary>
        public int MaxSourceRecords { get; init; } = 4096;
        /// <summary>Maximum sum of full ID 13 values in UTF-16 units before trimming. Default 1,048,576.</summary>
        public int MaxTotalSourceCharacters { get; init; } = 1_048_576;
        /// <summary>Maximum retained attempts, including explicit failures. Default 256.</summary>
        public int MaxDeclarations { get; init; } = 256;
        /// <summary>Maximum stored adapter messages; omitted messages never hide incompleteness. Default 128.</summary>
        public int MaxDiagnostics { get; init; } = 128;
        /// <summary>Independent native parser limits for each attempted substring. Must not be null.</summary>
        public SpdxParseOptions ParseOptions { get; init; } = new();
        /// <summary>Native validation message budget; traversal retains all occurrences. Must not be null.</summary>
        public SpdxValidationOptions ValidationOptions { get; init; } = new();

        internal void Check() {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxSourceRecords);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxTotalSourceCharacters);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxDeclarations);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxDiagnostics);
            ArgumentNullException.ThrowIfNull(ParseOptions);
            ArgumentNullException.ThrowIfNull(ValidationOptions);
            // Native validation helpers are internal. Check the pinned public constraints even for empty faces.
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ParseOptions.MaxInputLength);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ParseOptions.MaxIdentifierLength);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ParseOptions.MaxTokens);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ParseOptions.MaxNodes);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ParseOptions.MaxParenthesisDepth);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(ParseOptions.MaxParenthesisDepth, 256);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ValidationOptions.MaxDiagnostics);
        }
    }
}
