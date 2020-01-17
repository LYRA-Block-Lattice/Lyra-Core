using System;
using Microsoft.Extensions.Logging;
using GrpcServerHelper;
using Communication;

namespace Lyra.Node2
{
    public class MessageProcessor : MessageProcessorBase<RequestMessage, ResponseMessage>
    {
        public MessageProcessor(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public override string GetClientId(RequestMessage message) => message.ClientId;

        public override ResponseMessage Process(RequestMessage message)
        {
            if (string.IsNullOrEmpty(message.Payload))
                return null;

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
                    Payload = $"Response to \"{message.Payload}\"",
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
                    Payload = e.Message,
                    Status = MessageStatus.Error,
                };
            }
        }
    }
}
