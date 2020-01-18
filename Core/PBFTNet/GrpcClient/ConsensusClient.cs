using System;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;

namespace GrpcClient
{
    public class ConsensusClient
    {
        const int PORT = 4505;
        Client _client;

        public event EventHandler<string> OnMessage;

        public async Task<int> Start(string nodeAddress, string accountId)
        {
            Console.WriteLine("GrpcClient started.");

            var channelCredentials = new SslCredentials(File.ReadAllText(@"Certs\certificate.crt"));
            var channel = new Channel($"{nodeAddress}:{PORT}", channelCredentials);

            var nl = Environment.NewLine;
            var orgTextColor = Console.ForegroundColor;

            _client = new Client(accountId);
            await _client.Do(
                channel, 
                () =>
                {
                    Console.Write($"Connected to server.{nl}ClientId = ");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"{_client.ClientId}");
                    Console.ForegroundColor = orgTextColor;
                    Console.WriteLine($".{nl}Enter string message to server.{nl}" +
                        $"You will get response if your message will contain question mark '?'.{nl}" +
                        $"Enter empty message to quit.{nl}");
                },
                (resp) => { OnMessage(this, resp.Payload);  },
                () => Console.WriteLine("Shutting down...")
            );

            return 0;
        }

        public void SendMessage(object o)
        {
            _client.SendObject(o);
        }
    }
}
