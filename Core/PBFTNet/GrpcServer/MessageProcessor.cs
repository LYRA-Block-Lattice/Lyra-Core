using System;
using Microsoft.Extensions.Logging;
using GrpcServerHelper;
using Communication;
using Google.Protobuf;

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
            //if (string.IsNullOrEmpty(message.Payload))
            //    return null;

            switch(message.MessageId)
            {
                case "AuthorizerPrePrepare":
                case "AuthorizerPrepare":
                case "AuthorizerCommit":
                    OnPayload?.Invoke(this, (message.MessageId, message.Payload.ToByteArray()));
                    return null;
            }

            Logger.LogInformation($"To be processed: {message}");

            //
            // Request message processing should be placed here
            //

            if (message.Response != ResponseType.Required)
                return null;
            
            var timestamp = DateTime.UtcNow.Ticks;

            try
            {
                return new ResponseMessage
                {
                    ClientId = message.ClientId,
                    MessageId = message.MessageId,
                    Type = message.Type,
                    Time = timestamp,
                    Payload = ByteString.CopyFromUtf8(message.Payload.ToStringUtf8() == "\"ping\"" ? "\"pong\"" : $"\"Response to {message.Payload}\""),
                    Status = MessageStatus.Processed,
                };
            }
            catch (Exception e)
            {
                return new ResponseMessage
                {
                    ClientId = message.ClientId,
                    MessageId = message.MessageId,
                    Type = message.Type,
                    Time = timestamp,
                    Payload = ByteString.CopyFromUtf8(e.Message),
                    Status = MessageStatus.Error,
                };
            }
        }
    }
}
