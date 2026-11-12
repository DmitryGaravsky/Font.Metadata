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
        /// <summary>Maximum size of a retained metadata table; discarded WOFF2/MTX output still consumes decompression budgets.</summary>
        public int MaxTableBytes { get; init; } = DefaultMaxTableBytes;
        /// <summary>Maximum collection or EOT payload faces to inspect, retaining their original indexes.</summary>
        public int MaxFaces { get; init; } = DefaultMaxFaces;
        /// <summary>Maximum table directory entries to process per face.</summary>
        public int MaxTablesPerFace { get; init; } = DefaultMaxTablesPerFace;
        /// <summary>Maximum name records to inspect per distinct table, including invalid records.</summary>
        public int MaxNameRecords { get; init; } = DefaultMaxNameRecords;
        /// <summary>Maximum encoded string bytes attempted per name table (including language tags) or EOT header (including RootString).</summary>
        public int MaxNameStringBytes { get; init; } = DefaultMaxNameStringBytes;
        /// <summary>Maximum encoded string bytes attempted across all tables and EOT header strings; shared tables are counted once.</summary>
        public int MaxTotalNameStringBytes { get; init; } = DefaultMaxTotalNameStringBytes;
        /// <summary>Maximum reported diagnostics, including the final suppression diagnostic. Must be positive.</summary>
        public int MaxDiagnostics { get; init; } = DefaultMaxDiagnostics;
        /// <summary>Maximum bytes produced by all decompression stages, including discarded output and failed attempts.</summary>
        public int MaxTotalDecodedBytes { get; init; } = 128 * Mebibyte;
        /// <summary>Maximum combined LZ and optional RLE output of all streams in one MTX payload.</summary>
        public int MaxMtxPayloadBytes { get; init; } = 64 * Mebibyte;
        /// <summary>Maximum decoded work divided by compressed-unit length; independent of absolute limits.</summary>
        public int MaxExpansionRatio { get; init; } = 1000;
        /// <summary>Maximum global WOFF2 dictionary entries.</summary>
        public int MaxWoff2Tables { get; init; } = 16384;
        /// <summary>Maximum WOFF2 face-to-table indexes traversed, including skipped faces and duplicate references.</summary>
        public int MaxWoff2TableReferences { get; init; } = 1048576;
        /// <summary>Maximum live controlled decoded-buffer capacities, LZ windows and XOR copies; not a total heap limit.</summary>
        public int MaxRetainedDecodedBytes { get; init; } = 64 * Mebibyte;
        internal void Validate() {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxInputBytes, nameof(MaxInputBytes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxTableBytes, nameof(MaxTableBytes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxFaces, nameof(MaxFaces));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxTablesPerFace, nameof(MaxTablesPerFace));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxNameRecords, nameof(MaxNameRecords));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxNameStringBytes, nameof(MaxNameStringBytes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxTotalNameStringBytes, nameof(MaxTotalNameStringBytes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxDiagnostics, nameof(MaxDiagnostics));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxTotalDecodedBytes, nameof(MaxTotalDecodedBytes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxMtxPayloadBytes, nameof(MaxMtxPayloadBytes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxExpansionRatio, nameof(MaxExpansionRatio));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxWoff2Tables, nameof(MaxWoff2Tables));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxWoff2TableReferences, nameof(MaxWoff2TableReferences));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxRetainedDecodedBytes, nameof(MaxRetainedDecodedBytes));
            if(MaxInputBytes > Array.MaxLength - 1)
                throw new ArgumentOutOfRangeException(nameof(MaxInputBytes));
        }
    }
}
