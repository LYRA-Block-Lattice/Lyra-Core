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
using Lyra.Core.Utils;
using Lyra.Shared;
using Microsoft.Extensions.Logging;

namespace GrpcClient
{
    public class ConsensusClient
    {
        const int PORT = 4505;
        GrpcChannel _channel;
        GrpcClient _client;
        ILogger _log;
        string _clientId;
        string _accountId;
        string _ip;

        public event EventHandler<ResponseMessage> OnMessage;
        public event EventHandler<(string ip, string accountId)> OnShutdown;

        readonly BlockingCollection<(string type, byte[] payload)> _sendQueue = new BlockingCollection<(string type, byte[] payload)>();
        readonly ConcurrentDictionary<string, PendingMessage> _pendingMessages = new ConcurrentDictionary<string, PendingMessage>();

        // status
        private bool _connected;
        private bool _hasConfirmation;
        private int _retryConnectCount;

        public bool Connected { get => _connected; }
        public bool HasConfirmation { get => _hasConfirmation;}
        public string Ip { get => _ip; }

        public void Start(string nodeAddress, string AccountId, string clientId)
        {
            _log = new SimpleLogger("ConsensusClient").Logger;

            _log.LogInformation($"GrpcClient started for {nodeAddress}");

            _accountId = AccountId;
            _ip = nodeAddress;
            _clientId = clientId;

            var httpClientHandler = new HttpClientHandler();
            // Return `true` to allow certificates that are untrusted/invalid
            httpClientHandler.ServerCertificateCustomValidationCallback = (a, b, c, d) => true;
            var httpClient = new HttpClient(httpClientHandler);
            //httpClient.Timeout = TimeSpan.FromMinutes(5);

            var channelCredentials = new SslCredentials(File.ReadAllText($@"Certs/certificate.crt"));
            //var channel = new Channel($"{nodeAddress}:{PORT}", channelCredentials);
            _channel = GrpcChannel.ForAddress($"https://{nodeAddress}:{PORT}", new GrpcChannelOptions { HttpClient = httpClient });

            _retryConnectCount = -1;
            Connect();
        }

        private void Connect()
        {
            _log.LogInformation($"Trying to connect to remote node {_ip}");
            _connected = false;
            _hasConfirmation = false;

            _ = Task.Run(async () =>
            {
                _retryConnectCount++;
                await Task.Delay(_retryConnectCount * 1000);        // prevent hammer
                _client = new GrpcClient(_clientId, _ip);
                _client.FeedMessage += (sender) => FeedMessageTo(sender);

                try
                {
                    await _client.Do(
                            _channel,
                            () =>
                            {
                                _connected = true;
                                _log.LogInformation($"Connected to remote node {_ip}");
                            },
                            (resp) => { ConfirmMessage(resp.MessageId); OnMessage(this, resp); },
                            () => {
                                _log.LogInformation($"Disconnected from remote node {_ip}");
                                if (!_client.Stop.IsCancellationRequested)
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
                    _log.LogInformation($"_client.Do: {ex.Message}");
                }
            });
        }

        private (string id, string type, byte[] payload) FeedMessageTo(object sender)
        {
            var retryOne = _pendingMessages.Values.OrderBy(x => x.sent).FirstOrDefault(a => DateTime.Now - a.sent > TimeSpan.FromMilliseconds(450));
            if (retryOne == null)
            {
                var msg = _sendQueue.Take();
                var guid = Guid.NewGuid().ToString();
                _pendingMessages.TryAdd(guid, new PendingMessage { id = guid, type = msg.type, payload = msg.payload });
                return (guid, msg.type, msg.payload);
            }
            else
            {
                _log.LogInformation($"Retry send one message to {_ip} {retryOne.id}. Pending: {_pendingMessages.Count} In Queue: {_sendQueue.Count}");
                retryOne.sent = DateTime.Now;
                retryOne.times++;
                return (retryOne.id, retryOne.type, retryOne.payload);
            }
        }

        public void Close()
        {
            _log.LogInformation("Disconnected.");
            if (_client != null)
            {
                _client.Stop.Cancel();
                _channel.Dispose();
                _channel = null;
                _client = null;
            }
        }

        public void SendMessage(string type, byte[] payload)
        {
            _sendQueue.Add((type, payload));

            if (_pendingMessages.Values.Any(a => a.times > 10) || 
                _pendingMessages.Values.Any(a => DateTime.Now - a.sent > TimeSpan.FromSeconds(20)))
            {
                // retry connection
                _log.LogInformation($"Connection to {_ip} is broken. reconnect... ");
                _client.Stop.Cancel();
                Connect();
            }
        }

        private void ConfirmMessage(string id)
        {
            PendingMessage pm;
            _pendingMessages.TryRemove(id, out pm);
            _hasConfirmation = true;
            //_log.LogInformation($"Confirmed from  {_ip} {id}");
        }
    }

    public class PendingMessage
    {
        public string id { get; set; }
        public string type { get; set; }
        public byte[] payload { get; set; }
        public DateTime sent { get; set; } = DateTime.Now;
        public int times { get; set; } = 0;
    }
}
