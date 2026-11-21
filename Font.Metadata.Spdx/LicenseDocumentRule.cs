using System;
using System.Threading;

namespace Fonts.Metadata {
    internal enum LicenseDocumentMatch {
        WholeLine,
        WholeLineIgnoreCase,
        WrappedPhrase,
        PrefixWithValue,
        PrefixWithExactSuffix,
        Separator
    }
    internal sealed record LicenseDocumentHeading(LicenseDocumentRule Rule, string? Identifier);
    internal readonly record struct LicenseDocumentFrontMatter(LicenseDocumentRule Rule, ulong Profiles);
    internal readonly record struct LicenseDocumentRule(LicenseDocumentMatch Kind, string Text, bool AfterCopyright, string? Suffix,
        int MinimumLength) {
        internal bool OverlapsHeading(LicenseDocumentRule other) {
            // Wrapped rules admit runs of whitespace. Test the other rule's literal spelling using the same matcher.
            if(Kind == LicenseDocumentMatch.WrappedPhrase)
                return ReadPhrase(other.Text, 0, Text, CancellationToken.None, out _);
            if(other.Kind == LicenseDocumentMatch.WrappedPhrase)
                return ReadPhrase(Text, 0, other.Text, CancellationToken.None, out _);
            var comparison = Kind == LicenseDocumentMatch.WholeLine && other.Kind == LicenseDocumentMatch.WholeLine ?
                StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return string.Equals(Text, other.Text, comparison);
        }
        internal bool TryMatch(string source, int start, int end, int next, bool afterCopyright, CancellationToken token,
            out (int End, int Next) range) {
            range = (end, next);
            if(AfterCopyright && !afterCopyright)
                return false;
            var line = source.AsSpan(start, end - start);
            return Kind switch {
                LicenseDocumentMatch.WholeLine => line.SequenceEqual(Text),
                LicenseDocumentMatch.WholeLineIgnoreCase => line.Equals(Text, StringComparison.OrdinalIgnoreCase),
                LicenseDocumentMatch.WrappedPhrase => ReadPhrase(source, start, Text, token, out range),
                LicenseDocumentMatch.PrefixWithValue => HasValue(line, Text),
                LicenseDocumentMatch.PrefixWithExactSuffix => line.StartsWith(Text, StringComparison.OrdinalIgnoreCase) &&
                    line[Text.Length..].TrimStart(' ').SequenceEqual(Suffix),
                LicenseDocumentMatch.Separator => IsSeparator(line, Text[0], MinimumLength, token),
                _ => throw new ArgumentOutOfRangeException(nameof(Kind))
            };
        }
        static bool ReadPhrase(string text, int start, string phrase, CancellationToken token, out (int End, int Next) range) {
            range = default;
            // Only complete, allowlisted phrases may cross a newline. Preserve the original range.
            int position = start;
            foreach(var word in phrase.AsSpan().Split(' ')) {
                if(position != start) {
                    int beforeSpace = position;
                    bool crossedLine = false;
                    while(position < text.Length && IsSpace(text[position])) {
                        if((position & 1023) == 0)
                            token.ThrowIfCancellationRequested();
                        crossedLine |= text[position] is '\r' or '\n';
                        position++;
                    }
                    if(position == beforeSpace || crossedLine &&
                        text.AsSpan(position).StartsWith(FontLicenseDocumentAnalyzer.DeclarationPrefix, StringComparison.Ordinal))
                        return false;
                }
                var expected = phrase.AsSpan()[word];
                if(!text.AsSpan(position).StartsWith(expected, StringComparison.OrdinalIgnoreCase))
                    return false;
                position += expected.Length;
            }
            int phraseEnd = position;
            while(position < text.Length && text[position] is ' ' or '\t') {
                if((position & 1023) == 0)
                    token.ThrowIfCancellationRequested();
                position++;
            }
            if(position < text.Length && text[position] is not ('\r' or '\n'))
                return false;
            if(position < text.Length && text[position++] == '\r' && position < text.Length && text[position] == '\n')
                position++;
            range = (phraseEnd, position);
            return true;
        }
        static bool IsSeparator(ReadOnlySpan<char> line, char symbol, int minimumLength, CancellationToken token) {
            if(line.Length < minimumLength || line[0] != symbol)
                return false;
            for(int i = 1; i < line.Length; i++) {
                if((i & 1023) == 0)
                    token.ThrowIfCancellationRequested();
                if(line[i] != symbol)
                    return false;
            }
            return true;
        }
        internal static bool IsSpace(char c) {
            return c is ' ' or '\t' or '\r' or '\n';
        }
        internal static bool HasValue(ReadOnlySpan<char> line, string prefix) {
            return line.Length > prefix.Length && line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
