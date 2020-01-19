using System;
using Grpc.Core;
using GrpcClientHelper;
using Communication;
using System.Collections.Concurrent;
using Lyra.Shared;
using Grpc.Net.Client;
using Google.Protobuf;
using System.Text;
using System.Linq;

namespace GrpcClient
{
    public class GrpcClient : GrpcClientBase<RequestMessage, ResponseMessage>
    {
        readonly BlockingCollection<(string type, byte[] payload)> _sendQueue = new BlockingCollection<(string type, byte[] payload)>();

        readonly ConcurrentDictionary<string, PendingMessage> _pendingMessages = new ConcurrentDictionary<string, PendingMessage>();
        
        public string ClientId { get; }

        public GrpcClient(string accountId)
        {
            ClientId = accountId;
        }

        public override AsyncDuplexStreamingCall<RequestMessage, ResponseMessage> CreateDuplexClient(GrpcChannel channel) =>
            new Messaging.MessagingClient(channel).CreateStreaming(/*new CallOptions(deadline: DateTime.Now.AddHours(1))*/);

        public void SendObject(string type, byte[] payload)
        {
            _sendQueue.Add((type, payload));
        }

        protected override void Confirm(string id)
        {
            PendingMessage pm;
            _pendingMessages.TryRemove(id, out pm);
        }

        public override RequestMessage CreateMessage(string id, string type, byte[] payload)
        {
            return new RequestMessage
            {
                ClientId = ClientId,
                MessageId = id,
                Type = type,
                Response = ResponseType.Required,
                Payload = ByteString.CopyFrom(payload)
            };
        }

        public override (string id, string type, byte[] payload) MessagePayload
        {
            get 
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
                    Console.WriteLine("Retry send one");
                    retryOne.sent = DateTime.Now;
                    return (retryOne.id, retryOne.type, retryOne.payload);
                }
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
}
