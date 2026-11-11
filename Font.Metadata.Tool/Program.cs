using System;
using System.Reflection;

namespace Fonts.Metadata.Tool {
    static class Program {
        static int Main(string[] args) {
            if(args.Length == 0 || (args.Length == 1 && (args[0] == "--help" || args[0] == "-h"))) {
                Console.Out.WriteLine("Font.Metadata.Tool");
                Console.Out.WriteLine("Usage: font-metadata --help | --version");
                Console.Out.WriteLine("Scaffold only: font inspection and plain/JSON reports are not implemented yet.");
                return 0;
            }
            if(args.Length == 1 && args[0] == "--version") {
                string? version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                Console.Out.WriteLine(version);
                return 0;
            }
            if(args[0] == "inspect") {
                Console.Error.WriteLine("Font inspection is not implemented in this scaffold.");
                return 1;
            }
            Console.Error.WriteLine("Unknown arguments. Use --help for the supported commands.");
            return 2;
        }
    }
}
