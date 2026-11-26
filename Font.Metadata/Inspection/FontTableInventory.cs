using System.Collections.Generic;
using Fonts.Metadata.Internal;
using System.Collections.ObjectModel;

namespace Fonts.Metadata {
    /// <summary>Readable table directory declarations in source order.</summary>
    public sealed class FontTableInventory {
        internal FontTableInventory(FontSectionStatus status, List<FontTableEntry>? entries = null) {
            Status = status;
            Entries = entries == null ? ReadOnlyCollection<FontTableEntry>.Empty : ResultList<FontTableEntry>.Snapshot(entries);
        }
        /// <summary>Completeness of directory declarations independently of table contents.</summary>
        public FontSectionStatus Status { get; }
        /// <summary>Unambiguous declarations in the face directory.</summary>
        public IReadOnlyList<FontTableEntry> Entries { get; }
    }
    /// <summary>A directory entry, independent of whether the table contents could be decoded.</summary>
    public sealed class FontTableEntry {
        internal FontTableEntry(string tag, long length, long storedLength, bool compressed, int? transform, bool transformed) {
            Tag = tag;
            Length = length;
            StoredLength = storedLength;
            IsCompressed = compressed;
            TransformVersion = transform;
            IsTransformed = transformed;
        }
        /// <summary>Four original tag bytes mapped one-to-one to characters, including spaces.</summary>
        public string Tag { get; }
        /// <summary>Declared original length in bytes (CTF length for MTX).</summary>
        public long Length { get; }
        /// <summary>Stored representation length. For WOFF2 this is measured before shared Brotli compression.</summary>
        public long StoredLength { get; }
        /// <summary>True when this table is individually compressed in WOFF.</summary>
        public bool IsCompressed { get; }
        /// <summary>Raw two-bit WOFF2 transform version, otherwise null.</summary>
        public int? TransformVersion { get; }
        /// <summary>True for a WOFF2 entry that declares a non-null transform.</summary>
        public bool IsTransformed { get; }
    }
}
