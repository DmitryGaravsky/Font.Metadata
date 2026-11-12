using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fonts.Metadata.Tool {
    static class Program {
        static async Task<int> Main(string[] args) {
            Console.OutputEncoding = new UTF8Encoding(false);
            using(var cancellation = new CancellationTokenSource()) {
                ConsoleCancelEventHandler handler = (_, eventArgs) => {
                    eventArgs.Cancel = true;
                    cancellation.Cancel();
                };
                Console.CancelKeyPress += handler;
                try {
                    return await Command.RunAsync(args, Console.Error, cancellation.Token);
                }
                finally {
                    Console.CancelKeyPress -= handler;
                }
            }
        }
    }
}
