using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using GrpcServerHelper;
using Communication;

namespace GrpcServer.Services
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

        public void Dispose()
        {
            _gs.Dispose();
        }
    }
}
