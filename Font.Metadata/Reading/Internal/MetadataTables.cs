using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System;
using static Fonts.Metadata.Internal.NameTableDefinitions;

namespace Fonts.Metadata.Internal {
    internal static class MetadataTables {
        const string NameTableTag = "name", Os2TableTag = "OS/2";
        const int Os2VersionOffset = 0, LastSupportedOs2Version = 5;
        const int EmbeddingPermissionsOffset = 8;
        const int EmbeddingPermissionsEnd = EmbeddingPermissionsOffset + sizeof(ushort);
        const int VendorIdOffset = 58, VendorIdLength = 4;
        const int Os2MetadataPrefixSize = VendorIdOffset + VendorIdLength;
        const byte FirstPrintableAscii = 0x20, LastPrintableAscii = 0x7e;

        static readonly Encoding Unicode = new UnicodeEncoding(true, false, true);
        internal static ushort U16(ReadOnlySpan<byte> data, int offset) {
            return BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, sizeof(ushort)));
        }
        internal static uint U32(ReadOnlySpan<byte> data, int offset) {
            return BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, sizeof(uint)));
        }

        // name formats 0/1: six-byte header, twelve-byte name records and optional four-byte language records.
        // Offsets into string storage are relative to storageOffset, not to the table origin.
        // https://learn.microsoft.com/en-us/typography/opentype/spec/name
        internal static TableMetadata ReadNames(ReadOnlySpan<byte> table, int origin, ReadingContext context) {
            var reader = new NameReader(table, origin, context);
            return reader.Read();
        }
        internal static TableMetadata ReadOs2(ReadOnlySpan<byte> table, int origin, ReadingContext context) {
            context.Token.ThrowIfCancellationRequested();
            var diagnostics = new DiagnosticBag(context.Limits.MaxDiagnostics);
            void Error(string code, string message, int relative, FontDiagnosticCategory category = FontDiagnosticCategory.MalformedData) {
                var diagnostic = new FontDiagnostic(code, FontDiagnosticSeverity.Error, category, message,
                    tableTag: Os2TableTag, byteOffset: origin + relative);
                diagnostics.Add(diagnostic);
            }
            if(table.Length < sizeof(ushort)) {
                Error(FontDiagnosticCodes.InvalidOs2Table, Messages.Os2VersionTruncated, 0);
                return new TableMetadata { Status = FontSectionStatus.Invalid, Diagnostics = diagnostics.Freeze() };
            }
            if(U16(table, Os2VersionOffset) > LastSupportedOs2Version) {
                Error(FontDiagnosticCodes.UnsupportedTableVersion, Messages.UnsupportedOs2Version, 0, FontDiagnosticCategory.UnsupportedFeature);
                return new TableMetadata { Status = FontSectionStatus.Unsupported, Diagnostics = diagnostics.Freeze() };
            }
            // Metadata prefix shared by OS/2 versions 0..5: fsType at 8, achVendID at 58 (four bytes).
            // Later metrics are outside this reader's contract. https://learn.microsoft.com/en-us/typography/opentype/spec/os2
            ushort? permissions = table.Length >= EmbeddingPermissionsEnd ? U16(table, EmbeddingPermissionsOffset) : null;
            string? vendor = null;
            if(table.Length < Os2MetadataPrefixSize) {
                Error(FontDiagnosticCodes.InvalidOs2Table, Messages.Os2PrefixTruncated, table.Length);
            }
            else {
                var vendorBytes = table.Slice(VendorIdOffset, VendorIdLength);
                if(IsVendorIdReadable(vendorBytes))
                    vendor = NameSelection.Clean(Encoding.ASCII.GetString(vendorBytes));
                else
                    Error(FontDiagnosticCodes.InvalidOs2Table, Messages.InvalidVendorId, VendorIdOffset);
            }
            return new TableMetadata {
                Status = GetOs2Status(diagnostics.HasErrors, permissions.HasValue),
                Permissions = permissions,
                VendorId = vendor,
                Diagnostics = diagnostics.Freeze()
            };
        }
        static bool IsVendorIdReadable(ReadOnlySpan<byte> bytes) {
            // achVendID is a four-byte identifier, not a platform-encoded name string.
            // Retain the existing tolerance for NUL padding without guessing non-ASCII bytes.
            // https://learn.microsoft.com/en-us/typography/opentype/spec/os2#achvendid
            foreach(byte value in bytes) {
                if(value != 0 && (value < FirstPrintableAscii || value > LastPrintableAscii))
                    return false;
            }
            return true;
        }
        static FontSectionStatus GetOs2Status(bool hasErrors, bool hasPermissions) {
            if(!hasErrors)
                return FontSectionStatus.Complete;
            return hasPermissions ? FontSectionStatus.Partial : FontSectionStatus.Invalid;
        }
        static bool UsesUnicodeEncoding(ushort platform, ushort encoding) {
            return platform switch {
                UnicodePlatformId => encoding <= LastSupportedUnicodeEncodingId,
                WindowsPlatformId => encoding is WindowsSymbolEncodingId or WindowsUnicodeBmpEncodingId or WindowsUnicodeFullEncodingId,
                IsoPlatformId => encoding == IsoUnicodeEncodingId,
                _ => false
            };
        }
        static Encoding? GetEncoding(ushort platform, ushort encoding) {
            // A name encoding ID is scoped to its platform. Unicode name strings use UTF-16BE,
            // even where the corresponding cmap encoding covers the full Unicode repertoire.
            // https://learn.microsoft.com/en-us/typography/opentype/spec/name#platform-encoding-and-language-ids
            if(UsesUnicodeEncoding(platform, encoding))
                return Unicode;
            int? codePage = (platform, encoding) switch {
                (MacintoshPlatformId, MacRomanEncodingId) => CodePages.MacRoman,
                (WindowsPlatformId, WindowsShiftJisEncodingId) => CodePages.ShiftJis,
                (WindowsPlatformId, WindowsPrcEncodingId) => CodePages.SimplifiedChinese,
                (WindowsPlatformId, WindowsBig5EncodingId) => CodePages.TraditionalChinese,
                (WindowsPlatformId, WindowsWansungEncodingId) => CodePages.KoreanWansung,
                (WindowsPlatformId, WindowsJohabEncodingId) => CodePages.KoreanJohab,
                (IsoPlatformId, IsoAsciiEncodingId) => CodePages.Ascii,
                (IsoPlatformId, IsoLatin1EncodingId) => CodePages.Latin1,
                _ => null
            };
            if(!codePage.HasValue)
                return null;
            if(platform == IsoPlatformId)
                return Encoding.GetEncoding(codePage.Value, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            // Use the provider directly; never register a process-wide encoding provider.
            return CodePagesEncodingProvider.Instance.GetEncoding(codePage.Value, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }
        static class CodePages {
            internal const int MacRoman = 10000;
            internal const int ShiftJis = 932;
            internal const int SimplifiedChinese = 936;
            internal const int TraditionalChinese = 950;
            internal const int KoreanWansung = 949;
            internal const int KoreanJohab = 1361;
            internal const int Ascii = 20127;
            internal const int Latin1 = 28591;
        }
        static class Messages {
            internal const string Os2VersionTruncated = "The OS/2 version is truncated.";
            internal const string UnsupportedOs2Version = "The OS/2 version is not supported.";
            internal const string Os2PrefixTruncated = "The OS/2 metadata prefix is truncated.";
            internal const string InvalidVendorId = "The OS/2 vendor identifier contains non-ASCII characters.";
            internal const string NameHeaderTruncated = "The name table header is truncated.";
            internal const string UnsupportedNameVersion = "The name table version is not supported.";
            internal const string InvalidNameStorage = "The name records or string storage are outside the table or overlap.";
            internal const string LanguageCountTruncated = "The language-tag count is truncated.";
            internal const string InvalidLanguageDirectory = "Language-tag records overlap storage or extend outside the table.";
            internal const string InvalidLanguageString = "A language-tag string is outside the table or has odd UTF-16 length.";
            internal const string UnavailableLanguageTag = "A name record references an unavailable language tag.";
            internal const string NameRecordLimit = "MaxNameRecords prevented reading all name records.";
            internal const string NameStringOutOfBounds = "A name string is outside the table.";
            internal const string UnsupportedNameEncoding = "The name platform/encoding combination is not supported.";
            internal const string NameStringBudget = "A name string exceeds the remaining per-table or container string budget.";
            internal const string InvalidEncodedString = "A name string is not valid in its declared encoding.";
        }
        ref struct NameReader {
            const int HeaderSize = 6, RecordSize = 12;
            const int FormatOffset = 0, RecordCountOffset = 2, StorageOffsetField = 4;
            const int FormatWithLanguageTags = 1;
            const int PlatformOffset = 0, EncodingOffset = 2, LanguageOffset = 4;
            const int NameIdOffset = 6, StringLengthOffset = 8, StringOffsetField = 10;
            const int LanguageRecordSize = 4, LanguageStringOffsetField = 2;
            const int Utf16CodeUnitSize = sizeof(ushort);

            readonly ReadOnlySpan<byte> table;
            readonly int origin;
            readonly ReadingContext context;
            readonly DiagnosticBag diagnostics;
            readonly Dictionary<(int Offset, int Length, int CodePage), string?> strings;
            readonly List<FontMetadata.NameRecord> records;
            long decodedBytes;
            bool malformed, unsupported, limited;
            internal NameReader(ReadOnlySpan<byte> table, int origin, ReadingContext context) {
                this.table = table;
                this.origin = origin;
                this.context = context;
                diagnostics = new DiagnosticBag(context.Limits.MaxDiagnostics);
                strings = new();
                records = new();
            }
            void Error(string code, string message, int offset, FontDiagnosticCategory category = FontDiagnosticCategory.MalformedData) {
                malformed |= category == FontDiagnosticCategory.MalformedData;
                unsupported |= category is FontDiagnosticCategory.UnsupportedFeature or FontDiagnosticCategory.UnsupportedEncoding;
                limited |= category == FontDiagnosticCategory.LimitExceeded;
                var diagnostic = new FontDiagnostic(code, FontDiagnosticSeverity.Error, category, message,
                    tableTag: NameTableTag, byteOffset: (long)origin + offset);
                diagnostics.Add(diagnostic);
            }
            TableMetadata Result() {
                var status = GetStatus();
                var recordSnapshot = Array.AsReadOnly(records.ToArray());
                var preferredNames = NameSelection.Select(records, context.Token);
                var diagnosticSnapshot = diagnostics.Freeze();
                return new TableMetadata {
                    Status = status,
                    Records = recordSnapshot,
                    PreferredNames = preferredNames,
                    Diagnostics = diagnosticSnapshot
                };
            }
            FontSectionStatus GetStatus() {
                if(!malformed && !unsupported && !limited && !diagnostics.Truncated)
                    return FontSectionStatus.Complete;
                if(records.Count > 0)
                    return FontSectionStatus.Partial;
                if(malformed)
                    return FontSectionStatus.Invalid;
                return limited ? FontSectionStatus.NotRead : FontSectionStatus.Unsupported;
            }
            bool HasValidRecordStorage(int recordsEnd, int storage, bool hasRecords) {
                if(recordsEnd > table.Length || storage > table.Length)
                    return false;
                return !hasRecords || storage >= recordsEnd;
            }
            bool IsStringInBounds(int offset, int length) {
                return offset <= table.Length - length;
            }
            bool FitsStringBudgets(int length) {
                return length <= context.Limits.MaxNameStringBytes - decodedBytes &&
                    length <= context.Limits.MaxTotalNameStringBytes - context.NameStringBytes;
            }
            internal TableMetadata Read() {
                if(table.Length < HeaderSize) {
                    Error(FontDiagnosticCodes.InvalidNameTable, Messages.NameHeaderTruncated, 0);
                    return Result();
                }
                int format = U16(table, FormatOffset);
                int count = U16(table, RecordCountOffset);
                int storage = U16(table, StorageOffsetField);
                if(format > FormatWithLanguageTags) {
                    Error(FontDiagnosticCodes.UnsupportedTableVersion, Messages.UnsupportedNameVersion, 0, FontDiagnosticCategory.UnsupportedFeature);
                    return Result();
                }
                int recordsEnd = HeaderSize + count * RecordSize;
                if(!HasValidRecordStorage(recordsEnd, storage, count > 0)) {
                    Error(FontDiagnosticCodes.InvalidNameTable, Messages.InvalidNameStorage, StorageOffsetField);
                    return Result();
                }
                if(!TryReadLanguageDirectory(format, count, recordsEnd, storage, out int languageCount))
                    return Result();
                ValidateLanguageStrings(recordsEnd, languageCount, storage);
                int allowed = Math.Min(count, context.Limits.MaxNameRecords);
                for(int i = 0; i < allowed; i++) {
                    context.Token.ThrowIfCancellationRequested();
                    int pos = HeaderSize + i * RecordSize;
                    ushort platform = U16(table, pos + PlatformOffset), encodingId = U16(table, pos + EncodingOffset);
                    ushort language = U16(table, pos + LanguageOffset), nameId = U16(table, pos + NameIdOffset);
                    int length = U16(table, pos + StringLengthOffset), offset = storage + U16(table, pos + StringOffsetField);
                    string? value = Decode(offset, length, GetEncoding(platform, encodingId), pos);
                    if(value == null)
                        continue;
                    string? languageTag = ReadLanguageTag(format, language, languageCount, recordsEnd, storage, pos);
                    records.Add(new FontMetadata.NameRecord(i, nameId, platform, encodingId, language, languageTag, value));
                }
                if(allowed < count)
                    Error(FontDiagnosticCodes.LimitExceeded, Messages.NameRecordLimit, HeaderSize + allowed * RecordSize, FontDiagnosticCategory.LimitExceeded);
                return Result();
            }
            bool TryReadLanguageDirectory(int format, int count, int recordsEnd, int storage, out int languageCount) {
                languageCount = 0;
                if(format != FormatWithLanguageTags)
                    return true;
                if(recordsEnd + sizeof(ushort) > table.Length) {
                    Error(FontDiagnosticCodes.InvalidNameTable, Messages.LanguageCountTruncated, recordsEnd);
                    return false;
                }
                languageCount = U16(table, recordsEnd);
                int languagesEnd = recordsEnd + sizeof(ushort) + languageCount * LanguageRecordSize;
                if(!HasValidRecordStorage(languagesEnd, storage, count > 0 || languageCount > 0)) {
                    Error(FontDiagnosticCodes.InvalidNameTable, Messages.InvalidLanguageDirectory, recordsEnd);
                    return false;
                }
                return true;
            }
            static int GetLanguageRecordPosition(int recordsEnd, int index) {
                return recordsEnd + sizeof(ushort) + index * LanguageRecordSize;
            }
            void ValidateLanguageStrings(int recordsEnd, int languageCount, int storage) {
                // The language array follows the name records, but each string offset is relative
                // to the shared storage area. Unused tags are checked without consuming decode budgets.
                // https://learn.microsoft.com/en-us/typography/opentype/spec/name#naming-table-version-1
                for(int i = 0; i < languageCount; i++) {
                    context.Token.ThrowIfCancellationRequested();
                    int pos = GetLanguageRecordPosition(recordsEnd, i);
                    int length = U16(table, pos), offset = storage + U16(table, pos + LanguageStringOffsetField);
                    if(!IsStringInBounds(offset, length) || length % Utf16CodeUnitSize != 0)
                        Error(FontDiagnosticCodes.InvalidNameTable, Messages.InvalidLanguageString, pos);
                }
            }
            string? ReadLanguageTag(int format, ushort language, int languageCount, int recordsEnd, int storage, int recordPosition) {
                if(language < FirstLanguageTagId)
                    return null;
                int index = language - FirstLanguageTagId;
                if(format != FormatWithLanguageTags || index >= languageCount) {
                    Error(FontDiagnosticCodes.InvalidNameTable, Messages.UnavailableLanguageTag, recordPosition + LanguageOffset);
                    return null;
                }
                int pos = GetLanguageRecordPosition(recordsEnd, index);
                return Decode(storage + U16(table, pos + LanguageStringOffsetField), U16(table, pos), Unicode, pos);
            }
            string? Decode(int offset, int length, Encoding? encoding, int recordOffset) {
                if(!IsStringInBounds(offset, length)) {
                    Error(FontDiagnosticCodes.InvalidNameTable, Messages.NameStringOutOfBounds, recordOffset);
                    return null;
                }
                if(encoding == null) {
                    Error(FontDiagnosticCodes.UnsupportedEncoding, Messages.UnsupportedNameEncoding, recordOffset, FontDiagnosticCategory.UnsupportedEncoding);
                    return null;
                }
                var key = (offset, length, encoding.CodePage);
                if(strings.TryGetValue(key, out string? previous))
                    return previous;
                if(!FitsStringBudgets(length)) {
                    Error(FontDiagnosticCodes.LimitExceeded, Messages.NameStringBudget, recordOffset, FontDiagnosticCategory.LimitExceeded);
                    return null;
                }
                decodedBytes += length;
                context.NameStringBytes += length;
                string? value = null;
                try { value = encoding.GetString(table.Slice(offset, length)); }
                catch(DecoderFallbackException) {
                    Error(FontDiagnosticCodes.InvalidNameTable, Messages.InvalidEncodedString, offset);
                }
                strings[key] = value;
                return value;
            }
        }
    }
}
