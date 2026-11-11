using System;

namespace Fonts.Metadata {
    /// <summary>Immutable processing budgets. These bound work, not the exact managed heap size.</summary>
    public sealed class FontReadLimits {
        const int Mebibyte = 1024 * 1024;
        const int DefaultMaxInputBytes = 128 * Mebibyte;
        const int DefaultMaxTableBytes = 16 * Mebibyte;
        const int DefaultMaxFaces = 256;
        const int DefaultMaxTablesPerFace = 4096;
        const int DefaultMaxNameRecords = 4096;
        const int DefaultMaxNameStringBytes = 4 * Mebibyte;
        const int DefaultMaxTotalNameStringBytes = 16 * Mebibyte;
        const int DefaultMaxDiagnostics = 256;

        /// <summary>Maximum input bytes. Oversized inputs are rejected before parsing; streams probe at most one additional byte.</summary>
        public int MaxInputBytes { get; init; } = DefaultMaxInputBytes;
        /// <summary>Maximum size of a supported metadata table; unrelated outline tables are not decoded.</summary>
        public int MaxTableBytes { get; init; } = DefaultMaxTableBytes;
        /// <summary>Maximum collection faces to inspect, retaining their original indexes.</summary>
        public int MaxFaces { get; init; } = DefaultMaxFaces;
        /// <summary>Maximum table directory entries to process per face.</summary>
        public int MaxTablesPerFace { get; init; } = DefaultMaxTablesPerFace;
        /// <summary>Maximum name records to inspect per distinct table, including invalid records.</summary>
        public int MaxNameRecords { get; init; } = DefaultMaxNameRecords;
        /// <summary>Maximum distinct encoded string bytes decoded per name table, including language tags.</summary>
        public int MaxNameStringBytes { get; init; } = DefaultMaxNameStringBytes;
        /// <summary>Maximum distinct encoded name string bytes decoded across the container; shared tables are counted once.</summary>
        public int MaxTotalNameStringBytes { get; init; } = DefaultMaxTotalNameStringBytes;
        /// <summary>Maximum reported diagnostics, including the final suppression diagnostic. Must be positive.</summary>
        public int MaxDiagnostics { get; init; } = DefaultMaxDiagnostics;
        internal void Validate() {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxInputBytes, nameof(MaxInputBytes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxTableBytes, nameof(MaxTableBytes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxFaces, nameof(MaxFaces));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxTablesPerFace, nameof(MaxTablesPerFace));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxNameRecords, nameof(MaxNameRecords));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxNameStringBytes, nameof(MaxNameStringBytes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxTotalNameStringBytes, nameof(MaxTotalNameStringBytes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxDiagnostics, nameof(MaxDiagnostics));
            if(MaxInputBytes > Array.MaxLength - 1)
                throw new ArgumentOutOfRangeException(nameof(MaxInputBytes));
        }
    }
}
