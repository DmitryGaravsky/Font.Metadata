using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Fonts.Metadata.Tool {
    internal static class StandardOutput {
        const int WindowsOutputHandle = -11;
        const int UnixOutputDescriptor = 1;
        const int Unbuffered = 1;
        const string InvalidOutput = "The standard output handle is unavailable.";

        internal static Stream Open() {
            if(!Console.IsOutputRedirected)
                return Console.OpenStandardOutput();

            // Console streams suppress broken-pipe errors. Use a borrowed OS handle for redirected
            // output so the command can report CLI_OUTPUT_ERROR instead of a false success.
            // https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Console/src/System/ConsolePal.Windows.cs
            IntPtr value = OperatingSystem.IsWindows() ? GetStdHandle(WindowsOutputHandle) : new IntPtr(UnixOutputDescriptor);
            var handle = new SafeFileHandle(value, ownsHandle: false);
            try {
                if(handle.IsInvalid)
                    throw new IOException(InvalidOutput);
                return new FileStream(handle, FileAccess.Write, Unbuffered, isAsync: false);
            }
            catch {
                handle.Dispose();
                throw;
            }
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int standardHandle);
    }
}
