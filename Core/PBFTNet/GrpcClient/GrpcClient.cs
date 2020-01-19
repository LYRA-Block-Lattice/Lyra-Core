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
    public delegate (string id, string type, byte[] payload) MessageSupply(object sender);
    public class GrpcClient : GrpcClientBase<RequestMessage, ResponseMessage>
    {
        public event MessageSupply FeedMessage;
        public string ClientId { get; }
        public string Ip { get; }

        public GrpcClient(string accountId, string IP)
        {
            ClientId = accountId;
            Ip = IP;
        }

        public override AsyncDuplexStreamingCall<RequestMessage, ResponseMessage> CreateDuplexClient(GrpcChannel channel) =>
            new Messaging.MessagingClient(channel).CreateStreaming(/*new CallOptions(deadline: DateTime.Now.AddHours(1))*/);

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
                return FeedMessage.Invoke(this);
            }
        }
    }
}
