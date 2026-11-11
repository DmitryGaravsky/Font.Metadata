using System;
using System.Collections.Generic;
using System.Text;

namespace Fonts.Metadata.Internal {
    internal static class LicenseText {
        const string AnnotationPrefix = "SPDX-License-Identifier: ";
        internal static List<LicenseRules.Match> MatchRecord(string value) {
            ReadOnlySpan<char> text = TrimEdges(value.AsSpan());
            var matches = new List<LicenseRules.Match>();
            var seen = new HashSet<LicenseRules.Match>();
            var header = MatchHeader(text);
            Add(header, matches, seen);
            int start = 0, depth = 0, quoteDepth = 0;
            char quote = '\0', quoteStart = '\0';
            bool address = false, annotationLineChecked = false;
            for(int i = 0; i < text.Length; i++) {
                char c = text[i];
                if(quoteDepth > 0) {
                    if(c == quoteStart && quoteStart != quote)
                        quoteDepth++;
                    else if(c == quote)
                        quoteDepth--;
                    continue;
                }
                if(char.IsWhiteSpace(c))
                    address = false;
                if(c == ':' && text.Slice(i).StartsWith("://", StringComparison.Ordinal))
                    address = true;
                if(address && c == ';' && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
                    address = false;
                if(address)
                    continue;
                quote = GetClosingQuote(text, i);
                if(quote != '\0') {
                    quoteStart = c;
                    quoteDepth = 1;
                    continue;
                }
                if(c == '(')
                    depth++;
                else if(c == ')' && depth > 0)
                    depth--;
                if(depth != 0)
                    continue;
                if(c is '\r' or '\n' && !annotationLineChecked) {
                    // Only the first nonempty line of an independent statement can be an annotation.
                    // Inspect it once, so many newlines in rejected prose do not cause quadratic rescanning.
                    string line = Normalize(text.Slice(start, i - start));
                    var annotation = MatchAnnotation(line);
                    bool accepted = annotation.HasValue && !IsContinuation(text.Slice(i + 1));
                    if(accepted || line.Length == 0) {
                        Add(annotation, matches, seen);
                        start = i + 1;
                    }
                    else
                        annotationLineChecked = true;
                    continue;
                }
                if(!IsStatementEnd(text, i))
                    continue;
                bool whole = start == 0 && i == text.Length - 1;
                int length = i - start + (c is '!' or '?' ? 1 : 0);
                var statement = MatchStatement(text.Slice(start, length), whole);
                Add(statement, matches, seen);
                start = i + 1;
                annotationLineChecked = false;
            }
            var remaining = MatchStatement(text.Slice(start), start == 0);
            Add(remaining, matches, seen);
            return matches;
        }
        static void Add(LicenseRules.Match? match, List<LicenseRules.Match> matches, HashSet<LicenseRules.Match> seen) {
            if(match.HasValue && seen.Add(match.Value))
                matches.Add(match.Value);
        }
        static char GetClosingQuote(ReadOnlySpan<char> text, int index) {
            char c = text[index];
            if(c == '\'' && (index == 0 || !char.IsLetterOrDigit(text[index - 1])))
                return '\'';
            return c switch {
                '"' => '"',
                '\u0060' => '\u0060',
                '\u201c' => '\u201d',
                '\u2018' => '\u2019',
                '\u201e' => '\u201c',
                '\u201a' => '\u2018',
                '\u00ab' => '\u00bb',
                '\u2039' => '\u203a',
                _ => '\0'
            };
        }
        static bool IsStatementEnd(ReadOnlySpan<char> text, int index) {
            char c = text[index];
            if(c == ';')
                return true;
            // A decimal point or an internal dot in an identifier is not a sentence boundary.
            bool punctuation = c is '.' or '!' or '?';
            return punctuation && (index + 1 == text.Length || char.IsWhiteSpace(text[index + 1]));
        }
        static LicenseRules.Match? MatchAnnotation(string text) {
            if(!text.StartsWith(AnnotationPrefix, StringComparison.OrdinalIgnoreCase))
                return null;
            string identifier = text.Substring(AnnotationPrefix.Length);
            return LicenseRules.MatchIdentifier(identifier);
        }
        static LicenseRules.Match? MatchStatement(ReadOnlySpan<char> statement, bool wholeRecord) {
            string text = Normalize(statement);
            if(text.Length == 0)
                return null;
            if(text.StartsWith(AnnotationPrefix, StringComparison.OrdinalIgnoreCase))
                return MatchAnnotation(text);
            const string urlPrefix = "License: ";
            if(text.StartsWith(urlPrefix, StringComparison.OrdinalIgnoreCase)) {
                string url = text.Substring(urlPrefix.Length);
                return LicenseUrls.Match(url);
            }
            bool declaration = RemovePrefix(ref text, "This Font Software is licensed under ") ||
                RemovePrefix(ref text, "This font is licensed under ") || RemovePrefix(ref text, "Licensed under ");
            if(!declaration && !wholeRecord)
                return null;
            if(declaration)
                RemovePrefix(ref text, "the ");
            if(text.Contains("://", StringComparison.Ordinal))
                return LicenseUrls.Match(text);
            // Whole-value lookup deliberately rejects negations, prose, unknown variants and compound expressions.
            // AND/OR/WITH must not be flattened: https://spdx.github.io/spdx-spec/v2.3.1/SPDX-license-expressions/
            return LicenseRules.MatchText(text);
        }
        static bool RemovePrefix(ref string value, string prefix) {
            if(!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            value = value.Substring(prefix.Length);
            return true;
        }
        static LicenseRules.Match? MatchHeader(ReadOnlySpan<char> text) {
            int newline = text.IndexOfAny('\r', '\n');
            if(newline < 0)
                return null;
            string first = Normalize(text.Slice(0, newline));
            ReadOnlySpan<char> rest = text.Slice(newline).TrimStart();
            int secondEnd = rest.IndexOfAny('\r', '\n');
            ReadOnlySpan<char> secondLine = secondEnd < 0 ? rest : rest.Slice(0, secondEnd);
            string second = Normalize(secondLine);
            var firstMatch = LicenseRules.MatchName(first);
            // A title must not be the subject of a continued sentence or an operand of a continued expression.
            if(firstMatch.HasValue && firstMatch.Value.Identifier != "MIT" && !IsContinuation(rest))
                return firstMatch;
            if(!second.StartsWith("Version ", StringComparison.OrdinalIgnoreCase))
                return null;
            string spaced = first + " " + second;
            string commaSeparated = first + ", " + second;
            string dashSeparated = first + " - " + second;
            var joined = LicenseRules.MatchName(spaced) ??
                LicenseRules.MatchName(commaSeparated) ?? LicenseRules.MatchName(dashSeparated);
            if(secondEnd >= 0 && IsContinuation(rest.Slice(secondEnd)))
                return null;
            return joined;
        }
        static bool IsContinuation(ReadOnlySpan<char> text) {
            text = text.TrimStart();
            if(text.IsEmpty)
                return false;
            if(text[0] is '/' or '&' or '|' or ',' or '(' or ')' or '+')
                return true;
            int end = 0;
            while(end < text.Length && char.IsAsciiLetter(text[end]))
                end++;
            ReadOnlySpan<char> word = text.Slice(0, end);
            // Word boundaries also reject malformed operator continuations such as WITH: or OR;.
            return word.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("OR", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("WITH", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("Version", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("not", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("is", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("are", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("was", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("were", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("for", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("only", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("compatible", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("rather", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("instead", StringComparison.OrdinalIgnoreCase);
        }
        static ReadOnlySpan<char> TrimEdges(ReadOnlySpan<char> value) {
            int start = 0, end = value.Length;
            while(start < end && (char.IsWhiteSpace(value[start]) || value[start] == '\0'))
                start++;
            while(end > start && (char.IsWhiteSpace(value[end - 1]) || value[end - 1] == '\0'))
                end--;
            return value.Slice(start, end - start);
        }
        static string Normalize(ReadOnlySpan<char> value) {
            value = value.Trim();
            var result = new StringBuilder(value.Length);
            bool space = false;
            foreach(char c in value) {
                if(char.IsWhiteSpace(c)) {
                    space = result.Length > 0;
                    continue;
                }
                if(space)
                    result.Append(' ');
                result.Append(c);
                space = false;
            }
            return result.ToString();
        }
    }
}
