using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Fonts.Metadata.Internal {
    internal static class LicenseRules {
        internal const int IdRule = 1, NameRule = 2, FamilyRule = 3, UrlRule = 4;
        internal readonly record struct Match(string Identifier, FontLicenseIdentifierKind Kind, int Rule);
        // Explicit supported identifiers, not syntax validation or an inferred version.
        // Canonical identifiers and exception distinctions: https://spdx.org/licenses/
        static readonly FrozenDictionary<string, string> identifiers = new[] {
            "MIT", "Apache-2.0", "OFL-1.0", "OFL-1.1", "BSD-2-Clause", "BSD-3-Clause", "MPL-2.0", "BSL-1.0",
            "GPL-2.0-only", "GPL-2.0-or-later", "GPL-3.0-only", "GPL-3.0-or-later",
            "LGPL-2.1-only", "LGPL-2.1-or-later", "LGPL-3.0-only", "LGPL-3.0-or-later"
        }.ToFrozenDictionary(id => id, StringComparer.OrdinalIgnoreCase);
        static readonly FrozenDictionary<string, string> names = new Dictionary<string, string> {
            ["MIT License"] = "MIT",
            ["The MIT License"] = "MIT",
            ["Apache License, Version 2.0"] = "Apache-2.0",
            ["Apache License Version 2.0"] = "Apache-2.0",
            ["Apache License 2.0"] = "Apache-2.0",
            ["Apache 2.0"] = "Apache-2.0",
            ["SIL Open Font License, Version 1.0"] = "OFL-1.0",
            ["SIL Open Font License, Version 1.1"] = "OFL-1.1",
            ["BSD 2-Clause License"] = "BSD-2-Clause",
            ["BSD 3-Clause License"] = "BSD-3-Clause",
            ["Mozilla Public License 2.0"] = "MPL-2.0",
            ["Mozilla Public License Version 2.0"] = "MPL-2.0",
            ["Boost Software License 1.0"] = "BSL-1.0",
            ["Boost Software License - Version 1.0"] = "BSL-1.0"
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        static readonly FrozenDictionary<string, string> families = new Dictionary<string, string> {
            ["Apache License"] = "Apache",
            ["SIL Open Font License"] = "OFL",
            ["Open Font License"] = "OFL",
            ["BSD License"] = "BSD",
            ["GNU General Public License"] = "GPL",
            ["GNU General Public License version 2"] = "GPL",
            ["GPL"] = "GPL",
            ["GPL version 3"] = "GPL",
            ["GNU Lesser General Public License"] = "LGPL",
            ["GNU Lesser General Public License version 2.1"] = "LGPL",
            ["LGPL"] = "LGPL",
            ["Mozilla Public License"] = "MPL",
            ["Boost Software License"] = "Boost"
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        internal static string GetRuleId(int rule) {
            return rule switch {
                IdRule => "FLA001",
                NameRule => "FLA002",
                FamilyRule => "FLA003",
                UrlRule => "FLA004",
                _ => throw new ArgumentOutOfRangeException(nameof(rule))
            };
        }
        internal static Match? MatchIdentifier(string value) {
            if(identifiers.TryGetValue(value, out string? id))
                return new Match(id, FontLicenseIdentifierKind.Spdx, IdRule);
            return null;
        }
        internal static Match? MatchName(string value) {
            if(names.TryGetValue(value, out string? id))
                return new Match(id, FontLicenseIdentifierKind.Spdx, NameRule);
            return null;
        }
        internal static Match? MatchText(string value) {
            var exact = MatchIdentifier(value) ?? MatchName(value);
            if(exact.HasValue)
                return exact;
            if(families.TryGetValue(value, out string? family))
                return new Match(family, FontLicenseIdentifierKind.Family, FamilyRule);
            return null;
        }
    }
}
