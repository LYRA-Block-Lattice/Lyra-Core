using System;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;

namespace GrpcClient
{
    class Program
    {
        const int PORT = 4505;

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("GrpcClient started.");

            var channelCredentials = new SslCredentials(File.ReadAllText(@"Certs\certificate.crt"));
            var channel = new Channel($"localhost:{PORT}", channelCredentials);

            var nl = Environment.NewLine;
            var orgTextColor = Console.ForegroundColor;

            var client = new Client();
            await client.Do(
                channel, 
                () =>
                {
                    Console.Write($"Connected to server.{nl}ClientId = ");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"{client.ClientId}");
                    Console.ForegroundColor = orgTextColor;
                    Console.WriteLine($".{nl}Enter string message to server.{nl}" +
                        $"You will get response if your message will contain question mark '?'.{nl}" +
                        $"Enter empty message to quit.{nl}");
                },
                () => Console.WriteLine("Shutting down...")
            );

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

            return 0;
        }
    }
}
