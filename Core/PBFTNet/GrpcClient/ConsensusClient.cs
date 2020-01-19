using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
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

        readonly BlockingCollection<(string type, byte[] payload)> _sendQueue = new BlockingCollection<(string type, byte[] payload)>();
        readonly ConcurrentDictionary<string, PendingMessage> _pendingMessages = new ConcurrentDictionary<string, PendingMessage>();

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

            Connect();
        }

        private void Connect()
        {
            Console.Write($"Trying to connect to remote node {_ip}");
            _client = new GrpcClient(_accountId, _ip);
            _client.FeedMessage += (sender) =>
            {
                var retryOne = _pendingMessages.Values.FirstOrDefault(a => DateTime.Now - a.sent > TimeSpan.FromSeconds(2));
                if (retryOne == null)
                {
                    var msg = _sendQueue.Take();
                    var guid = Guid.NewGuid().ToString();
                    _pendingMessages.TryAdd(guid, new PendingMessage { id = guid, type = msg.type, payload = msg.payload });
                    return (guid, msg.type, msg.payload);
                }
                else
                {
                    Console.WriteLine($"Retry send one message to {_ip} {retryOne.id}");
                    retryOne.sent = DateTime.Now;
                    return (retryOne.id, retryOne.type, retryOne.payload);
                }
            };

            _ = Task.Run(async () =>
            {
                try
                {
                    await _client.Do(
                            _channel,
                            _stop.Token,
                            () =>
                            {
                                Console.Write($"Connected to remote node {_ip}");
                            },
                            (resp) => { ConfirmMessage(resp.MessageId); OnMessage(this, resp); },
                            () => {
                                Console.Write($"Disconnected from remote node {_ip}");
                                if (!_stop.IsCancellationRequested)
                                    Connect(); 
                                else
                                {
                                    Close();
                                    OnShutdown?.Invoke(this, (_ip, _accountId));
                                }
                            }
                        );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"_client.Do: {ex.Message}");
                }
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
            if (_client == null)
                OnShutdown?.Invoke(this, (_ip, _accountId));
            else
            {
                _sendQueue.Add((type, payload));
            }
        }

        private void ConfirmMessage(string id)
        {
            PendingMessage pm;
            _pendingMessages.TryRemove(id, out pm);
            Console.WriteLine($"Confirmed from  {_ip} {id}");
        }
    }

    public class PendingMessage
    {
        public string id { get; set; }
        public string type { get; set; }
        public byte[] payload { get; set; }
        public DateTime sent { get; set; } = DateTime.Now;
    }
}
