using System;
using System.Collections.Generic;
using Fonts.Metadata.Internal;
using static Fonts.Metadata.Internal.NameTableDefinitions;

namespace Fonts.Metadata {
    /// <summary>Analyzes already decoded name records without I/O, mutation or application licensing policy.</summary>
    public static class FontLicenseAnalyzer {
        /// <summary>
        /// Examines every available name ID 13/14 record, regardless of language or preferred-name selection.
        /// Only the documented finite vocabulary is recognized; compound expressions and exceptions are not simplified.
        /// </summary>
        /// <param name="face">The face whose source records are analyzed, including partially read faces.</param>
        /// <returns>Independent evidence and name-table completeness, not a final license or permission decision.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="face"/> is null.</exception>
        public static FontLicenseAnalysis Analyze(FontMetadata.Face face) {
            ArgumentNullException.ThrowIfNull(face);
            var hints = new List<FontLicenseHint>();
            // The reader can share decoded string objects among many records. Reuse matching work only within this call.
            var matchesByValue = new Dictionary<string, List<LicenseRules.Match>>(ReferenceEqualityComparer.Instance);
            foreach(var record in face.NameRecords) {
                if(record.NameId != LicenseDescriptionNameId && record.NameId != LicenseUrlNameId)
                    continue;
                if(!matchesByValue.TryGetValue(record.Value, out var matches)) {
                    matches = LicenseText.MatchRecord(record.Value);
                    matchesByValue.Add(record.Value, matches);
                }
                for(int rule = LicenseRules.IdRule; rule <= LicenseRules.UrlRule; rule++) {
                    string ruleId = LicenseRules.GetRuleId(rule);
                    foreach(var match in matches) {
                        if(match.Rule != rule)
                            continue;
                        var hint = new FontLicenseHint(match.Identifier, match.Kind, ruleId, record);
                        hints.Add(hint);
                    }
                }
            }
            bool complete = face.NameTableStatus is FontSectionStatus.Complete or FontSectionStatus.Missing;
            return new FontLicenseAnalysis(complete, hints);
        }
    }
}
