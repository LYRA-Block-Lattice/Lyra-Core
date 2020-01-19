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
        readonly BlockingCollection<object> _sendQueue = new BlockingCollection<object>();
        public string ClientId { get; }

        public GrpcClient(string accountId)
        {
            ClientId = accountId;
        }

        public override AsyncDuplexStreamingCall<RequestMessage, ResponseMessage> CreateDuplexClient(GrpcChannel channel) =>
            new Messaging.MessagingClient(channel).CreateStreaming(new CallOptions(deadline: DateTime.Now.AddHours(1)));

        public void SendObject(object o)
        {
            _sendQueue.Add(o);
        }

        public override RequestMessage CreateMessage(object ob)
        {
            if (ob is byte[])
            {
                return new RequestMessage
                {
                    ClientId = ClientId,
                    MessageId = $"{Guid.NewGuid()}",
                    Type = MessageType.Payload,
                    Time = DateTime.UtcNow.Ticks,
                    Response = ResponseType.Required,
                    Payload = ByteString.CopyFrom((byte[])ob)
                };
            }
            else
            {
                var payload = $"{ob}";
                return new RequestMessage
                {
                    ClientId = ClientId,
                    MessageId = $"{Guid.NewGuid()}",
                    Type = MessageType.Ordinary,
                    Time = DateTime.UtcNow.Ticks,
                    Response = ResponseType.Required,
                    Payload = ByteString.CopyFrom(payload, Encoding.UTF8)
                };
            }
        }

        public override string MessagePayload
        {
            get 
            {
                object msg = _sendQueue.Take();
                return msg.Json();
            }
        }
    }
}
