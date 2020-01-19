using System;
using Microsoft.Extensions.Logging;
using GrpcServerHelper;
using Communication;
using Google.Protobuf;
using Lyra.Shared;

namespace Lyra.Node2
{
    public class MessageProcessor : MessageProcessorBase<RequestMessage, ResponseMessage>
    {
        public event EventHandler<(string type, byte[] payload)> OnPayload;

        public MessageProcessor(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public override string GetClientId(RequestMessage message) => message.ClientId;

        // this default process becomes heartbeat.
        public override ResponseMessage Process(RequestMessage message)
        {
            switch(message.Type)
            {
                case "AuthorizerPrePrepare":
                case "AuthorizerPrepare":
                case "AuthorizerCommit":
                    OnPayload?.Invoke(this, (message.Type, message.Payload.ToByteArray()));
                    break;
            }

            Logger.LogInformation($"To be processed: {message.MessageId} from {message.ClientId.Shorten()}");

            //
            // Request message processing should be placed here
            //

            if (message.Response != ResponseType.Required)
                return null;
            
            return new ResponseMessage
            {
                ClientId = message.ClientId,
                MessageId = message.MessageId,
                Type = message.Type,
                Payload = ByteString.Empty,
                Status = MessageStatus.Processed,
            };
        }
    }
}
