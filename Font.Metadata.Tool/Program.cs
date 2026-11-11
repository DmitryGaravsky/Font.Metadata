using System;
using System.Reflection;

namespace Fonts.Metadata.Tool {
    static class Program {
        const string HelpOption = "--help", ShortHelpOption = "-h", VersionOption = "--version";
        const string InspectCommand = "inspect";
        const int SuccessExitCode = 0, UnavailableExitCode = 1, InvalidArgumentsExitCode = 2;

        static int Main(string[] args) {
            if(IsHelpRequest(args)) {
                Console.Out.WriteLine(Messages.Title);
                Console.Out.WriteLine(Messages.Usage);
                Console.Out.WriteLine(Messages.CurrentLimitations);
                return SuccessExitCode;
            }
            if(IsSingleArgument(args, VersionOption)) {
                string? version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                Console.Out.WriteLine(version);
                return SuccessExitCode;
            }
            if(args[0] == InspectCommand) {
                Console.Error.WriteLine(Messages.InspectionUnavailable);
                return UnavailableExitCode;
            }
            Console.Error.WriteLine(Messages.UnknownArguments);
            return InvalidArgumentsExitCode;
        }
        static bool IsSingleArgument(string[] args, string value) {
            return args.Length == 1 && args[0] == value;
        }
        static bool IsHelpRequest(string[] args) {
            return args.Length == 0 || IsSingleArgument(args, HelpOption) || IsSingleArgument(args, ShortHelpOption);
        }
        static class Messages {
            internal const string Title = "Font.Metadata.Tool";
            internal const string Usage = "Usage: font-metadata --help | --version";
            internal const string CurrentLimitations = "Font inspection and plain/JSON reports are not implemented in this CLI yet.";
            internal const string InspectionUnavailable = "Font inspection is not implemented in this CLI yet.";
            internal const string UnknownArguments = "Unknown arguments. Use --help for the supported commands.";
        }
    }
}
