using System.Collections.Generic;
using System.Collections.ObjectModel;
using static Fonts.Metadata.Internal.NameTableDefinitions;

namespace Fonts.Metadata {
    /// <summary>Immutable metadata from one font container. No input buffer or stream is retained.</summary>
    public sealed class FontMetadata {
        internal FontMetadata(FontFormat format, IReadOnlyList<Face>? faces = null,
            EotHeader? eot = null, FontSectionStatus? eotStatus = null) {
            Format = format;
            Eot = eot;
            EotHeaderStatus = eotStatus ?? (format == FontFormat.Eot ? FontSectionStatus.NotRead : FontSectionStatus.Missing);
            Faces = faces ?? ReadOnlyCollection<Face>.Empty;
        }
        /// <summary>Source container format, independently of its SFNT flavor.</summary>
        public FontFormat Format { get; }
        /// <summary>Faces in source order, including bounded placeholders for unreadable face directories.</summary>
        public IReadOnlyList<Face> Faces { get; }
        /// <summary>Verified EOT header facts, or null when absent or unavailable.</summary>
        public EotHeader? Eot { get; }
        /// <summary>Completeness of the EOT header; Missing for other container formats.</summary>
        public FontSectionStatus EotHeaderStatus { get; }
        /// <summary>Metadata of one face; table completeness is reported independently.</summary>
        public sealed class Face {
            internal Face(int index, uint? flavor, FontSectionStatus nameStatus = FontSectionStatus.NotRead,
                FontSectionStatus os2Status = FontSectionStatus.NotRead, IReadOnlyList<NameRecord>? records = null,
                string? vendorId = null, ushort? permissions = null, IReadOnlyDictionary<int, string>? preferred = null,
                FontFaceSource source = FontFaceSource.Container) {
                Index = index;
                Source = source;
                SfntFlavor = flavor;
                NameTableStatus = nameStatus;
                Os2TableStatus = os2Status;
                NameRecords = records ?? ReadOnlyCollection<NameRecord>.Empty;
                VendorId = vendorId;
                EmbeddingPermissions = permissions;
                preferred ??= ReadOnlyDictionary<int, string>.Empty;
                FamilyName = preferred.GetValueOrDefault(TypographicFamilyNameId) ?? preferred.GetValueOrDefault(FamilyNameId);
                SubfamilyName = preferred.GetValueOrDefault(TypographicSubfamilyNameId) ?? preferred.GetValueOrDefault(SubfamilyNameId);
                FullName = preferred.GetValueOrDefault(FullNameId);
                PostScriptName = preferred.GetValueOrDefault(PostScriptNameId);
                Version = preferred.GetValueOrDefault(VersionNameId);
                Copyright = preferred.GetValueOrDefault(CopyrightNameId);
                Trademark = preferred.GetValueOrDefault(TrademarkNameId);
                Manufacturer = preferred.GetValueOrDefault(ManufacturerNameId);
                Designer = preferred.GetValueOrDefault(DesignerNameId);
                Description = preferred.GetValueOrDefault(DescriptionNameId);
                VendorUrl = preferred.GetValueOrDefault(VendorUrlNameId);
                DesignerUrl = preferred.GetValueOrDefault(DesignerUrlNameId);
                LicenseDescription = preferred.GetValueOrDefault(LicenseDescriptionNameId);
                LicenseUrl = preferred.GetValueOrDefault(LicenseUrlNameId);
            }
            /// <summary>Original zero-based face index; recovery never renumbers faces.</summary>
            public int Index { get; }
            /// <summary>Container component supplying this face; EOT header facts remain separate.</summary>
            public FontFaceSource Source { get; }
            /// <summary>Original SFNT flavor, when the header was readable.</summary>
            public uint? SfntFlavor { get; }
            /// <summary>Completeness of the name table.</summary>
            public FontSectionStatus NameTableStatus { get; }
            /// <summary>Completeness of supported OS/2 metadata fields.</summary>
            public FontSectionStatus Os2TableStatus { get; }
            /// <summary>All decoded name IDs in source order, including duplicates and unknown IDs.</summary>
            public IReadOnlyList<NameRecord> NameRecords { get; }
            /// <summary>OS/2 vendor identifier, or null when unavailable or blank.</summary>
            public string? VendorId { get; }
            /// <summary>Raw OS/2 fsType bits; zero is distinct from missing. No licensing conclusion is implied.</summary>
            public ushort? EmbeddingPermissions { get; }
            /// <summary>Preferred FamilyName value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? FamilyName { get; }
            /// <summary>Preferred SubfamilyName value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? SubfamilyName { get; }
            /// <summary>Preferred FullName value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? FullName { get; }
            /// <summary>Preferred PostScriptName value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? PostScriptName { get; }
            /// <summary>Preferred Version value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? Version { get; }
            /// <summary>Preferred Copyright value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? Copyright { get; }
            /// <summary>Preferred Trademark value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? Trademark { get; }
            /// <summary>Preferred Manufacturer value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? Manufacturer { get; }
            /// <summary>Preferred Designer value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? Designer { get; }
            /// <summary>Preferred Description value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? Description { get; }
            /// <summary>Preferred VendorUrl value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? VendorUrl { get; }
            /// <summary>Preferred DesignerUrl value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? DesignerUrl { get; }
            /// <summary>Preferred LicenseDescription value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? LicenseDescription { get; }
            /// <summary>Preferred LicenseUrl value among decoded records; null when unavailable. Completeness follows NameTableStatus.</summary>
            public string? LicenseUrl { get; }
        }
        /// <summary>A decoded source name record. Its value is preserved without trimming or normalization.</summary>
        public sealed class NameRecord {
            internal NameRecord(int index, ushort nameId, ushort platformId, ushort encodingId, ushort languageId, string? languageTag, string value) {
                Index = index;
                NameId = nameId;
                PlatformId = platformId;
                EncodingId = encodingId;
                LanguageId = languageId;
                LanguageTag = languageTag;
                Value = value;
            }
            /// <summary>Original record index within the name table.</summary>
            public int Index { get; }
            /// <summary>OpenType name identifier, including identifiers unknown to this library.</summary>
            public ushort NameId { get; }
            /// <summary>Original platform identifier.</summary>
            public ushort PlatformId { get; }
            /// <summary>Original platform-specific encoding identifier.</summary>
            public ushort EncodingId { get; }
            /// <summary>Original language identifier.</summary>
            public ushort LanguageId { get; }
            /// <summary>Decoded format-1 language tag, or null when absent or unreadable.</summary>
            public string? LanguageTag { get; }
            /// <summary>Unmodified decoded string, including whitespace, NULs and empty values.</summary>
            public string Value { get; }
        }
        /// <summary>Verified EOT header facts, independent of all embedded faces.</summary>
        public sealed class EotHeader {
            internal EotHeader(uint? formatVersion, uint? flags, ushort? permissions,
                string? family, string? style, string? fullName, string? version,
                uint? eudcCodePage, uint? eudcFlags, uint? eudcSize) {
                FormatVersion = formatVersion;
                Flags = flags;
                EmbeddingPermissions = permissions;
                FamilyName = family;
                StyleName = style;
                FullName = fullName;
                Version = version;
                EudcCodePage = eudcCodePage;
                EudcFlags = eudcFlags;
                EudcFontDataSize = eudcSize;
            }
            /// <summary>Raw EOT container version, when verified.</summary>
            public uint? FormatVersion { get; }
            /// <summary>Raw main-payload processing flags; not a licensing conclusion.</summary>
            public uint? Flags { get; }
            /// <summary>Raw header fsType; independent of the embedded OS/2 value.</summary>
            public ushort? EmbeddingPermissions { get; }
            /// <summary>Unmodified UTF-16LE family name; null means unavailable, not an empty string.</summary>
            public string? FamilyName { get; }
            /// <summary>Unmodified UTF-16LE style name.</summary>
            public string? StyleName { get; }
            /// <summary>Unmodified UTF-16LE full name.</summary>
            public string? FullName { get; }
            /// <summary>Unmodified textual version name, distinct from FormatVersion.</summary>
            public string? Version { get; }
            /// <summary>Raw version-specific EUDC code page, or null if absent or unreadable.</summary>
            public uint? EudcCodePage { get; }
            /// <summary>Raw independent EUDC processing flags, including a verified zero.</summary>
            public uint? EudcFlags { get; }
            /// <summary>Declared EUDC payload size, including zero; null if absent or unreadable.</summary>
            public uint? EudcFontDataSize { get; }
        }
    }
}
