using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using GrpcServerHelper;
using Communication;

namespace Lyra.Node2.Services
{
    public class DuplexService : Messaging.MessagingBase, IDisposable
    {
        private GeneralGrpcService<RequestMessage, ResponseMessage> _gs;

        public DuplexService(ILoggerFactory loggerFactory, ServerGrpcSubscribers serverGrpcSubscribers, MessageProcessor messageProcessor)
        {
            _gs = new GeneralGrpcService<RequestMessage, ResponseMessage>(loggerFactory, serverGrpcSubscribers, messageProcessor);
        }

        public override async Task CreateStreaming(IAsyncStreamReader<RequestMessage> requestStream, IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        {
            await _gs.CreateDuplexStreaming(requestStream, responseStream, context);
        }

        public Task BroadcastAsync(string json)
        {
            var msg = new ResponseMessage
            {
                Payload = json
            };
            return _gs.BroadcastAsync(msg);
        }

        public void Dispose()
        {
            _gs.Dispose();
        }
    }
}
