using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fonts.Metadata {
    /// <summary>An immutable catalog of exact license document recognition profiles.</summary>
    public sealed class FontLicenseDocumentCatalog {
        const string ResourceName = "Font.Metadata.Spdx.LicenseProfiles.json";
        const int MaxJsonCharacters = 262_144;
        const int MaxProfiles = 64;
        const int MaxRules = 256;
        const int MaxDigestsPerProfile = 16;
        const int MaxRuleCharacters = 256;
        const int MaxIdentifierCharacters = 128;
        const string CatalogSizeMessage = "The license catalog exceeds the supported size.";
        const string InvalidProfileMessage = "A license profile is null or has an invalid identifier or source URL.";
        const string DuplicateIdentifierMessage = "License catalog identifiers must be unique.";
        const string InvalidDigestMessage = "Each license profile requires one to sixteen unique SHA-256 digests.";
        const string InvalidRuleMessage = "A license profile contains an invalid recognition rule.";
        const string AmbiguousHeadingMessage = "A heading cannot report conflicting identifiers.";
        const string MissingResourceMessage = "An embedded license profile could not be loaded.";
        static readonly JsonSerializerOptions SerializerOptions = new() {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowDuplicateProperties = false,
            MaxDepth = 8
        };
        readonly Profile?[] profiles;
        readonly FrozenDictionary<char, LicenseDocumentHeading[]> headings;
        readonly FrozenDictionary<string, BodyProfile> bodyProfiles;
        readonly LicenseDocumentFrontMatter[] frontMatter;
        FontLicenseDocumentCatalog(Profile?[] profiles) {
            if(profiles.Length > MaxProfiles)
                throw new JsonException(CatalogSizeMessage);
            this.profiles = profiles;
            InitialProfiles = profiles.Length == MaxProfiles ? ulong.MaxValue : (1UL << profiles.Length) - 1;
            var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var digests = new Dictionary<string, BodyProfile>(StringComparer.OrdinalIgnoreCase);
            var headingsByRule = new Dictionary<LicenseDocumentRule, LicenseDocumentHeading>();
            var frontMatterByRule = new Dictionary<LicenseDocumentRule, ulong>();
            int ruleCount = 0;
            for(int index = 0; index < profiles.Length; index++) {
                var profile = profiles[index];
                if(profile == null || !IsIdentifier(profile.Identifier) || !IsSource(profile.Source))
                    throw new JsonException(InvalidProfileMessage);
                if(!identifiers.Add(profile.Identifier))
                    throw new JsonException(DuplicateIdentifierMessage);
                ulong profileBit = 1UL << index;
                RegisterDigests(profile, profileBit, digests);
                if(profile.Headings == null || profile.FrontMatter == null ||
                    (long)ruleCount + profile.Headings.Length + profile.FrontMatter.Length > MaxRules)
                    throw new JsonException(CatalogSizeMessage);
                ruleCount += profile.Headings.Length + profile.FrontMatter.Length;
                foreach(var spec in profile.Headings) {
                    var rule = CompileRule(spec, heading: true);
                    string? identifier = spec!.Unversioned ? null : profile.Identifier;
                    if(headingsByRule.Values.Any(previous => previous.Identifier != identifier && rule.OverlapsHeading(previous.Rule)))
                        throw new JsonException(AmbiguousHeadingMessage);
                    headingsByRule.TryAdd(rule, new LicenseDocumentHeading(rule, identifier));
                }
                foreach(var spec in profile.FrontMatter) {
                    var rule = CompileRule(spec, heading: false);
                    frontMatterByRule.TryGetValue(rule, out ulong matchingProfiles);
                    frontMatterByRule[rule] = matchingProfiles | profileBit;
                }
            }
            headings = headingsByRule.Values.GroupBy(heading => char.ToUpperInvariant(heading.Rule.Text[0]))
                .ToFrozenDictionary(group => group.Key, group => group.ToArray());
            frontMatter = frontMatterByRule.Select(pair => new LicenseDocumentFrontMatter(pair.Key, pair.Value)).ToArray();
            bodyProfiles = digests.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Parses a JSON array of license profiles. By default the resulting catalog contains the supplied profiles.
        /// Set <paramref name="includeBuiltIn"/> to include the built-in profiles alongside them.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
        /// <exception cref="JsonException">The catalog has malformed JSON, invalid or ambiguous rules, or exceeds its limits.</exception>
        public static FontLicenseDocumentCatalog Parse(string json, bool includeBuiltIn = false) {
            ArgumentNullException.ThrowIfNull(json);
            if(json.Length > MaxJsonCharacters)
                throw new JsonException(CatalogSizeMessage);
            var customProfiles = JsonSerializer.Deserialize<Profile?[]>(json, SerializerOptions);
            if(customProfiles == null)
                throw new JsonException(InvalidProfileMessage);
            return new FontLicenseDocumentCatalog(includeBuiltIn ? [.. BuiltIn.profiles, .. customProfiles] : customProfiles);
        }
        internal static FontLicenseDocumentCatalog BuiltIn { get { return DefaultCatalog.Instance; } }
        internal ulong InitialProfiles { get; }
        internal ReadOnlySpan<LicenseDocumentFrontMatter> FrontMatter { get { return frontMatter; } }
        internal ReadOnlySpan<LicenseDocumentHeading> GetHeadings(char first) {
            return headings.GetValueOrDefault(char.ToUpperInvariant(first));
        }
        internal string? MatchBody(string digest, ulong candidates) {
            if(bodyProfiles.TryGetValue(digest, out var match) && (candidates & match.ProfileBit) != 0)
                return match.Identifier;
            return null;
        }
        static void RegisterDigests(Profile profile, ulong profileBit, Dictionary<string, BodyProfile> digests) {
            if(profile.BodySha256 == null || profile.BodySha256.Length is < 1 or > MaxDigestsPerProfile)
                throw new JsonException(InvalidDigestMessage);
            foreach(string? digest in profile.BodySha256) {
                if(digest == null || digest.Length != 64 || !digest.All(Uri.IsHexDigit) ||
                    !digests.TryAdd(digest, new BodyProfile(profile.Identifier, profileBit)))
                    throw new JsonException(InvalidDigestMessage);
            }
        }
        static LicenseDocumentRule CompileRule(RuleSpec? spec, bool heading) {
            if(spec == null || !IsRuleText(spec.Text))
                throw new JsonException(InvalidRuleMessage);
            var kind = spec.Match switch {
                "wholeLine" => LicenseDocumentMatch.WholeLine,
                "wholeLineIgnoreCase" => LicenseDocumentMatch.WholeLineIgnoreCase,
                "wrappedPhrase" => LicenseDocumentMatch.WrappedPhrase,
                "prefixWithValue" => LicenseDocumentMatch.PrefixWithValue,
                "prefixWithExactSuffix" => LicenseDocumentMatch.PrefixWithExactSuffix,
                "separator" => LicenseDocumentMatch.Separator,
                _ => throw new JsonException(InvalidRuleMessage)
            };
            bool headingKind = kind is LicenseDocumentMatch.WholeLine or LicenseDocumentMatch.WholeLineIgnoreCase or LicenseDocumentMatch.WrappedPhrase;
            if(heading && (!headingKind || spec.After != null) || !heading && spec.Unversioned ||
                spec.After is not (null or "copyright"))
                throw new JsonException(InvalidRuleMessage);
            bool hasSuffix = kind == LicenseDocumentMatch.PrefixWithExactSuffix;
            if(hasSuffix ? !IsRuleText(spec.Suffix) : spec.Suffix != null)
                throw new JsonException(InvalidRuleMessage);
            if(kind == LicenseDocumentMatch.Separator ? spec.Text.Length != 1 || spec.MinimumLength is not (>= 3 and <= MaxRuleCharacters) :
                spec.MinimumLength != null)
                throw new JsonException(InvalidRuleMessage);
            if(headingKind && (spec.Text[^1] is ' ' or '\t') ||
                kind == LicenseDocumentMatch.WrappedPhrase && (spec.Text.Contains('\t') || spec.Text.Contains("  ", StringComparison.Ordinal)))
                throw new JsonException(InvalidRuleMessage);
            return new LicenseDocumentRule(kind, spec.Text, spec.After != null, spec.Suffix, spec.MinimumLength.GetValueOrDefault());
        }
        static bool IsRuleText([NotNullWhen(true)] string? text) {
            return text is { Length: > 0 and <= MaxRuleCharacters } && !char.IsWhiteSpace(text[0]) &&
                !text.Any(c => c != '\t' && char.IsControl(c));
        }
        static bool IsIdentifier(string? identifier) {
            return identifier is { Length: > 0 and <= MaxIdentifierCharacters } && char.IsAsciiLetterOrDigit(identifier[0]) &&
                identifier.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-');
        }
        static bool IsSource(string? source) {
            return source == null || source.Length <= 1024 && Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                uri.Scheme is "https" or "http";
        }
        static FontLicenseDocumentCatalog LoadBuiltIn() {
            var assembly = typeof(FontLicenseDocumentCatalog).Assembly;
            using(var stream = assembly.GetManifestResourceStream(ResourceName) ?? throw new InvalidOperationException(MissingResourceMessage)) {
                var profiles = JsonSerializer.Deserialize<Profile?[]>(stream, SerializerOptions);
                if(profiles is not { Length: > 0 })
                    throw new InvalidOperationException(MissingResourceMessage);
                return new FontLicenseDocumentCatalog(profiles);
            }
        }
        static class DefaultCatalog {
            internal static readonly FontLicenseDocumentCatalog Instance = LoadBuiltIn();
        }
        sealed class Profile {
            public required string Identifier { get; init; }
            public string? Source { get; init; }
            public RuleSpec?[]? Headings { get; init; } = [];
            public RuleSpec?[]? FrontMatter { get; init; } = [];
            public required string?[]? BodySha256 { get; init; }
        }
        sealed class RuleSpec {
            public required string? Match { get; init; }
            public required string? Text { get; init; }
            public bool Unversioned { get; init; }
            public string? After { get; init; }
            public string? Suffix { get; init; }
            public int? MinimumLength { get; init; }
        }
        readonly record struct BodyProfile(string Identifier, ulong ProfileBit);
    }
}
