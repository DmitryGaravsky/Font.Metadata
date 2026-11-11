namespace Fonts.Metadata.Internal {
    // Shared semantic identifiers; byte-layout constants stay with the reader that owns them.
    // https://learn.microsoft.com/en-us/typography/opentype/spec/name#name-ids
    // https://learn.microsoft.com/en-us/typography/opentype/spec/name#platform-encoding-and-language-ids
    internal static class NameTableDefinitions {
        internal const ushort CopyrightNameId = 0;
        internal const ushort FamilyNameId = 1;
        internal const ushort SubfamilyNameId = 2;
        internal const ushort FullNameId = 4;
        internal const ushort VersionNameId = 5;
        internal const ushort PostScriptNameId = 6;
        internal const ushort TrademarkNameId = 7;
        internal const ushort ManufacturerNameId = 8;
        internal const ushort DesignerNameId = 9;
        internal const ushort DescriptionNameId = 10;
        internal const ushort VendorUrlNameId = 11;
        internal const ushort DesignerUrlNameId = 12;
        internal const ushort LicenseDescriptionNameId = 13;
        internal const ushort LicenseUrlNameId = 14;
        internal const ushort TypographicFamilyNameId = 16;
        internal const ushort TypographicSubfamilyNameId = 17;
        internal const ushort UnicodePlatformId = 0;
        internal const ushort MacintoshPlatformId = 1;
        internal const ushort IsoPlatformId = 2;
        internal const ushort WindowsPlatformId = 3;
        internal const ushort MacRomanEncodingId = 0;
        internal const ushort WindowsSymbolEncodingId = 0;
        internal const ushort WindowsUnicodeBmpEncodingId = 1;
        internal const ushort WindowsShiftJisEncodingId = 2;
        internal const ushort WindowsPrcEncodingId = 3;
        internal const ushort WindowsBig5EncodingId = 4;
        internal const ushort WindowsWansungEncodingId = 5;
        internal const ushort WindowsJohabEncodingId = 6;
        internal const ushort WindowsUnicodeFullEncodingId = 10;
        internal const ushort IsoAsciiEncodingId = 0;
        internal const ushort IsoUnicodeEncodingId = 1;
        internal const ushort IsoLatin1EncodingId = 2;
        internal const ushort LastSupportedUnicodeEncodingId = 6;
        internal const ushort EnglishUnitedStatesLanguageId = 0x0409;
        internal const ushort FirstLanguageTagId = 0x8000;
    }
}
