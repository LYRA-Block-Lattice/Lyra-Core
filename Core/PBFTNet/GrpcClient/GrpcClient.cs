using System;
using Grpc.Core;
using GrpcClientHelper;
using Communication;
using System.Collections.Concurrent;
using Lyra.Shared;
using Grpc.Net.Client;
using Google.Protobuf;
using System.Text;

namespace GrpcClient
{
    public class GrpcClient : GrpcClientBase<RequestMessage, ResponseMessage>
    {
        readonly BlockingCollection<(string type, byte[] payload)> _sendQueue = new BlockingCollection<(string type, byte[] payload)>();
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

        public override RequestMessage CreateMessage(string type, byte[] payload)
        {
            return new RequestMessage
            {
                ClientId = ClientId,
                MessageId = type,
                Type = MessageType.Payload,
                Time = DateTime.UtcNow.Ticks,
                Response = ResponseType.Required,
                Payload = ByteString.CopyFrom(payload)
            };
        }

        public override (string type, byte[] payload) MessagePayload
        {
            get 
            {
                var msg = _sendQueue.Take();
                return msg;
            }
        }
    }
}
