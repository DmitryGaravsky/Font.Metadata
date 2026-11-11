using System;

namespace Fonts.Metadata.Internal {
    internal static class LicenseUrls {
        internal static LicenseRules.Match? Match(string value) {
            // Validate the spelling before System.Uri canonicalizes percent escapes, backslashes or dot segments.
            // URI structure: https://www.rfc-editor.org/rfc/rfc3986#section-3
            foreach(char c in value) {
                if(char.IsWhiteSpace(c) || char.IsControl(c) || c is '%' or '\\' or '?' or '#' or '@')
                    return null;
            }
            int schemeEnd = value.IndexOf("://", StringComparison.Ordinal);
            if(schemeEnd < 0)
                return null;
            int pathStart = value.IndexOf('/', schemeEnd + 3);
            if(pathStart < 0)
                return null;
            string path = value.Substring(pathStart);
            if(!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
                !string.Equals(path, uri.AbsolutePath, StringComparison.Ordinal))
                return null;
            string? id = null;
            var kind = FontLicenseIdentifierKind.Spdx;
            switch(uri.Host.ToLowerInvariant()) {
                case "apache.org":
                case "www.apache.org":
                    if(path is "/licenses/LICENSE-2.0" or "/licenses/LICENSE-2.0.txt")
                        id = "Apache-2.0";
                    break;
                case "opensource.org":
                case "www.opensource.org":
                    if(path is "/licenses/MIT" or "/licenses/mit-license.php" or "/license/mit")
                        id = "MIT";
                    break;
                case "scripts.sil.org":
                    if(path is "/OFL" or "/OFL/") {
                        id = "OFL";
                        kind = FontLicenseIdentifierKind.Family;
                    }
                    break;
                case "spdx.org":
                    const string prefix = "/licenses/", suffix = ".html";
                    if(path.StartsWith(prefix, StringComparison.Ordinal) && path.EndsWith(suffix, StringComparison.Ordinal)) {
                        string candidate = path.Substring(prefix.Length, path.Length - prefix.Length - suffix.Length);
                        var match = LicenseRules.MatchIdentifier(candidate);
                        if(match.HasValue && string.Equals(candidate, match.Value.Identifier, StringComparison.Ordinal))
                            id = candidate;
                    }
                    break;
            }
            if(id == null)
                return null;
            return new LicenseRules.Match(id, kind, LicenseRules.UrlRule);
        }
    }
}
