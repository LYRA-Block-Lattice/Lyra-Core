using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;

namespace GrpcClient
{
    public class ConsensusClient
    {
        const int PORT = 4505;
        Client _client;

        public event EventHandler<string> OnMessage;

        public void Start(string nodeAddress, string accountId)
        {
            Console.WriteLine("GrpcClient started.");

            var httpClientHandler = new HttpClientHandler();
            // Return `true` to allow certificates that are untrusted/invalid
            httpClientHandler.ServerCertificateCustomValidationCallback = (a, b, c, d) => true;
            var httpClient = new HttpClient(httpClientHandler);

            var channelCredentials = new SslCredentials(File.ReadAllText(@"Certs\certificate.crt"));
            //var channel = new Channel($"{nodeAddress}:{PORT}", channelCredentials);
            var channel = GrpcChannel.ForAddress($"https://{nodeAddress}:{PORT}", new GrpcChannelOptions { HttpClient = httpClient });

            var nl = Environment.NewLine;
            var orgTextColor = Console.ForegroundColor;

            _client = new Client(accountId);

            _ = Task.Run(async () =>
            {
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
                    (resp) => { OnMessage(this, resp.Payload); },
                    () => Console.WriteLine("Shutting down...")
                );
            });
        }

        public void SendMessage(object o)
        {
            _client.SendObject(o);
        }
    }
}
