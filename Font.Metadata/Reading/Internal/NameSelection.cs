using System.Collections.Generic;
using System.Threading;
using System;
using static Fonts.Metadata.Internal.NameTableDefinitions;

namespace Fonts.Metadata.Internal {
    internal static class NameSelection {
        const int EnglishWindowsPriority = 3;
        const int UnicodePriority = 2;
        const int MacintoshPriority = 1;
        const int OtherPlatformPriority = 0;

        internal static Dictionary<int, string> Select(IReadOnlyList<FontMetadata.NameRecord> records, CancellationToken token) {
            var result = new Dictionary<int, string>();
            var scores = new Dictionary<int, int>();
            // Decoded strings are already shared by range/encoding. Reference equality avoids
            // rescanning long duplicate strings, including blank strings that never win selection.
            var cleaned = new Dictionary<string, string?>(ReferenceEqualityComparer.Instance);
            foreach(var record in records) {
                token.ThrowIfCancellationRequested();
                if(!CanSelect(record))
                    continue;
                int score = GetScore(record);
                if(scores.TryGetValue(record.NameId, out int previous) && score <= previous)
                    continue;
                if(!cleaned.TryGetValue(record.Value, out string? value)) {
                    value = Clean(record.Value);
                    cleaned.Add(record.Value, value);
                }
                if(value == null)
                    continue;
                scores[record.NameId] = score;
                result[record.NameId] = value;
            }
            token.ThrowIfCancellationRequested();
            return result;
        }
        static bool CanSelect(FontMetadata.NameRecord record) {
            // Keep raw values for diagnostics, but do not select a record whose required
            // language tag could not be read. OpenType name table, version 1:
            // https://learn.microsoft.com/en-us/typography/opentype/spec/name#naming-table-version-1
            return record.NameId <= TypographicSubfamilyNameId &&
                (record.LanguageId < FirstLanguageTagId || record.LanguageTag != null);
        }
        static int GetScore(FontMetadata.NameRecord record) {
            bool english = record.LanguageId == EnglishUnitedStatesLanguageId ||
                string.Equals(record.LanguageTag, "en-US", StringComparison.OrdinalIgnoreCase);
            if(record.PlatformId == WindowsPlatformId && english)
                return EnglishWindowsPriority;
            if(record.PlatformId is UnicodePlatformId or WindowsPlatformId)
                return UnicodePriority;
            return record.PlatformId == MacintoshPlatformId ? MacintoshPriority : OtherPlatformPriority;
        }
        internal static string? Clean(string value) {
            int start = 0, end = value.Length;
            while(start < end && (value[start] == '\0' || char.IsWhiteSpace(value[start])))
                start++;
            while(end > start && (value[end - 1] == '\0' || char.IsWhiteSpace(value[end - 1])))
                end--;
            return start == end ? null : value.Substring(start, end - start);
        }
    }
}
