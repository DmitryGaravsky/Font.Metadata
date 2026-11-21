using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Fonts.Metadata {
    internal sealed class LicenseDocumentBody {
        const int MaxNormalizedBodyLength = 16_384;
        readonly FontLicenseDocumentCatalog catalog;
        ulong candidates;
        bool afterCopyright;
        int bodyStart = -1, bodyEnd;
        internal LicenseDocumentBody(FontLicenseDocumentCatalog catalog) {
            this.catalog = catalog;
            candidates = catalog.InitialProfiles;
        }
        internal LicenseDocumentHeading? ReadHeading(string text, int start, ref int end, ref int next, CancellationToken token) {
            LicenseDocumentHeading? heading = null;
            int longestEnd = start;
            int longestNext = next;
            foreach(var candidate in catalog.GetHeadings(text[start])) {
                if(candidate.Rule.TryMatch(text, start, end, next, afterCopyright: false, token, out var range) && range.End > longestEnd) {
                    heading = candidate;
                    longestEnd = range.End;
                    longestNext = range.Next;
                }
            }
            if(heading != null) {
                end = longestEnd;
                next = longestNext;
            }
            return heading;
        }
        internal void Observe(string text, int start, ref int end, ref int next, bool heading, bool declaration, CancellationToken token) {
            if(bodyStart >= 0) {
                bodyEnd = end;
                return;
            }
            if(declaration)
                return;
            bodyEnd = end;
            // A heading supplies independent evidence even when it contradicts the body.
            if(heading) {
                afterCopyright = false;
                return;
            }
            var line = text.AsSpan(start, end - start);
            bool copyright = LicenseDocumentRule.HasValue(line, "Copyright ") || LicenseDocumentRule.HasValue(line, "© ");
            ulong matchingProfiles = 0;
            int longestEnd = start;
            int longestNext = next;
            foreach(var frontMatter in catalog.FrontMatter) {
                if(!frontMatter.Rule.TryMatch(text, start, end, next, afterCopyright, token, out var range) || range.End < longestEnd)
                    continue;
                if(range.End > longestEnd) {
                    matchingProfiles = 0;
                    longestEnd = range.End;
                    longestNext = range.Next;
                }
                matchingProfiles |= frontMatter.Profiles;
            }
            if(matchingProfiles != 0) {
                candidates &= matchingProfiles;
                end = longestEnd;
                next = longestNext;
            }
            else if(!copyright) {
                bodyStart = start;
            }
            afterCopyright = copyright;
        }
        internal (string Identifier, int Start, int End)? Match(string text, CancellationToken token) {
            if(bodyStart < 0 || candidates == 0)
                return null;
            string? digest = Fingerprint(text, token);
            if(digest != null && catalog.MatchBody(digest, candidates) is { } identifier)
                return (identifier, bodyStart, bodyEnd);
            return null;
        }
        string? Fingerprint(string text, CancellationToken token) {
            var normalized = new StringBuilder(Math.Min(bodyEnd - bodyStart, MaxNormalizedBodyLength));
            bool pendingSpace = false;
            for(int i = bodyStart; i < bodyEnd; i++) {
                if((i & 1023) == 0)
                    token.ThrowIfCancellationRequested();
                char c = text[i];
                if(LicenseDocumentRule.IsSpace(c)) {
                    pendingSpace = true;
                    continue;
                }
                if(pendingSpace)
                    normalized.Append(' ');
                normalized.Append(c is '\u201C' or '\u201D' ? '"' : c);
                if(normalized.Length > MaxNormalizedBodyLength)
                    return null;
                pendingSpace = false;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(normalized.ToString());
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
    }
}
