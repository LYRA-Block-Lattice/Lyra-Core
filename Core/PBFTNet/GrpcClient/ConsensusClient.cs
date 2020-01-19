using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using Grpc.Core;
using Grpc.Net.Client;

namespace GrpcClient
{
    public class ConsensusClient
    {
        const int PORT = 4505;
        GrpcChannel _channel;
        GrpcClient _client;
        string _accountId;
        string _ip;

        CancellationTokenSource _stop;

        public event EventHandler<ResponseMessage> OnMessage;
        public event EventHandler<(string ip, string accountId)> OnShutdown;

        public void Start(string nodeAddress, string accountId)
        {
            Console.WriteLine($"GrpcClient started for {nodeAddress}");

            _accountId = accountId;
            _ip = nodeAddress;
            _stop = new CancellationTokenSource();

            var httpClientHandler = new HttpClientHandler();
            // Return `true` to allow certificates that are untrusted/invalid
            httpClientHandler.ServerCertificateCustomValidationCallback = (a, b, c, d) => true;
            var httpClient = new HttpClient(httpClientHandler);
            //httpClient.Timeout = TimeSpan.FromMinutes(5);

            var channelCredentials = new SslCredentials(File.ReadAllText(@"Certs\certificate.crt"));
            //var channel = new Channel($"{nodeAddress}:{PORT}", channelCredentials);
            _channel = GrpcChannel.ForAddress($"https://{nodeAddress}:{PORT}", new GrpcChannelOptions { HttpClient = httpClient });

            var nl = Environment.NewLine;
            var orgTextColor = Console.ForegroundColor;

            _client = new GrpcClient(accountId);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _client.Do(
                            _channel,
                            _stop.Token,
                            () =>
                            {
                                Console.Write($"Connected to server.{nl}ClientId = ");
                                                //Console.ForegroundColor = ConsoleColor.Cyan;
                                                //Console.Write($"{_client.ClientId}");
                                                //Console.ForegroundColor = orgTextColor;
                                                //Console.WriteLine($".{nl}Enter string message to server.{nl}" +
                                                //    $"You will get response if your message will contain question mark '?'.{nl}" +
                                                //    $"Enter empty message to quit.{nl}");
                                            },
                            (resp) => { _client.Confirm(resp.MessageId); OnMessage(this, resp); }
                        );
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"_client.Do: {ex.Message}");
                }

                Close();
                OnShutdown?.Invoke(this, (_ip, _accountId));
            });
        }

        public void Close()
        {
            Console.WriteLine("Disconnected.");
            if (_client != null)
            {
                _stop.Cancel();
                _channel.Dispose();
                _channel = null;
                _client = null;
            }
        }

        public void SendMessage(string type, byte[] payload)
        {
            if(_client == null)
                OnShutdown?.Invoke(this, (_ip, _accountId));
            else
                _client.SendObject(type, payload);
        }
    }
}
