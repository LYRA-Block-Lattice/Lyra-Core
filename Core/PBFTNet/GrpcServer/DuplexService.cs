using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using GrpcServerHelper;
using Communication;
using Google.Protobuf;

namespace Lyra.Node2.Services
{
    public class DuplexService : Messaging.MessagingBase, IDisposable
    {
        private GeneralGrpcService<RequestMessage, ResponseMessage> _gs;
        public MessageProcessor Processor { get; set; }

        public DuplexService(ILoggerFactory loggerFactory, ServerGrpcSubscribers serverGrpcSubscribers, MessageProcessor messageProcessor)
        {
            Processor = messageProcessor;
            _gs = new GeneralGrpcService<RequestMessage, ResponseMessage>(loggerFactory, serverGrpcSubscribers, messageProcessor);
        }

        public override async Task CreateStreaming(IAsyncStreamReader<RequestMessage> requestStream, IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        {
            try
            {
                await _gs.CreateDuplexStreaming(requestStream, responseStream, context);
            }
            catch(Exception ex)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
            }            
        }

        public Task BroadcastAsync(string id, string msgtype, byte[] payload)
        {
            var msg = new ResponseMessage
            {
                MessageId = id,
                Type = msgtype,
                Payload = ByteString.CopyFrom(payload),
            };
            return _gs.BroadcastAsync(msg);
        }

        public void Dispose()
        {
            _gs.Dispose();
        }
    }
}
