using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Fonts.Metadata.Tool {
    internal sealed record InputSource(string? Path) {
        internal string Kind { get { return Path == null ? "stdin" : "file"; } }
    }

    internal sealed class CommandOptions {
        internal bool Help { get; init; }
        internal bool Version { get; init; }
        internal bool Json { get; init; }
        internal bool IncludeNameRecords { get; init; }
        internal bool AnalyzeLicenses { get; init; }
        internal bool AnalyzeSpdx { get; init; }
        internal int MaxInputBytes { get; init; } = new FontReadLimits().MaxInputBytes;
        internal IReadOnlyList<InputSource> Sources { get; init; } = ReadOnlyCollection<InputSource>.Empty;

        internal static bool TryParse(string[] args, out CommandOptions options) {
            options = new CommandOptions();
            if(args.Length == 0 || args.Length == 1 && IsHelp(args[0]) ||
                args.Length == 2 && args[0] == "inspect" && IsHelp(args[1])) {
                options = new CommandOptions { Help = true };
                return true;
            }
            if(args.Length == 1 && args[0] == "--version") {
                options = new CommandOptions { Version = true };
                return true;
            }
            if(args.Length == 0 || args[0] != "inspect")
                return false;
            bool literal = false, json = false, records = false, licenses = false, spdx = false;
            int maxInput = options.MaxInputBytes;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var sources = new List<InputSource>();
            for(int i = 1; i < args.Length; i++) {
                string argument = args[i];
                if(!literal && argument == "--") {
                    literal = true;
                    continue;
                }
                if(!literal && argument == "-") {
                    sources.Add(new InputSource(null));
                    continue;
                }
                if(!literal && argument.StartsWith('-')) {
                    if(!seen.Add(argument))
                        return false;
                    switch(argument) {
                        case "--include-name-records":
                            records = true;
                            break;
                        case "--analyze-spdx":
                            spdx = true;
                            break;
                        case "--analyze-licenses":
                            licenses = true;
                            break;
                        case "--format":
                            if(++i == args.Length || args[i] is not ("plain" or "json"))
                                return false;
                            json = args[i] == "json";
                            break;
                        case "--max-input-bytes":
                            if(++i == args.Length || !TryLimit(args[i], out maxInput))
                                return false;
                            break;
                        default:
                            return false;
                    }
                    continue;
                }
                if(string.IsNullOrWhiteSpace(argument) || argument.Contains('\0'))
                    return false;
                sources.Add(new InputSource(argument));
            }
            if(sources.Count == 0 || sources.Count > 1 && sources.Exists(source => source.Path == null))
                return false;
            options = new CommandOptions {
                Json = json,
                IncludeNameRecords = records,
                AnalyzeLicenses = licenses,
                AnalyzeSpdx = spdx,
                MaxInputBytes = maxInput,
                Sources = sources.AsReadOnly()
            };
            return true;
        }
        static bool IsHelp(string value) {
            return value is "--help" or "-h";
        }
        static bool TryLimit(string value, out int limit) {
            limit = 0;
            if(value.Length == 0)
                return false;
            foreach(char character in value) {
                if(character is < '0' or > '9')
                    return false;
            }
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) &&
                limit > 0 && limit < Array.MaxLength;
        }
    }
}
