using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Grpc.Core;

namespace GrpcServerHelper
{
    public class GeneralGrpcService<TRequest, TResponse>
    {
        private readonly ServerGrpcSubscribersBase<TResponse> _serverGrpcSubscribers;
        private readonly MessageProcessorBase<TRequest, TResponse> _messageProcessor;

        protected ILogger Logger { get; set; }

        public GeneralGrpcService(
            ILoggerFactory loggerFactory, 
            ServerGrpcSubscribersBase<TResponse> serverGrpcSubscribers, 
            MessageProcessorBase<TRequest, TResponse> messageProcessor)
        {
            _serverGrpcSubscribers = serverGrpcSubscribers;
            _messageProcessor = messageProcessor;
            Logger = loggerFactory.CreateLogger<GeneralGrpcService<TRequest, TResponse>>();
        }

        public async Task CreateDuplexStreaming(
            IAsyncStreamReader<TRequest> requestStream, 
            IServerStreamWriter<TResponse> responseStream, 
            ServerCallContext context)
        {
            var httpContext = context.GetHttpContext();
            Logger.LogInformation($"Connection id: {httpContext.Connection.Id}");

            // handshake. client send his id and signature.
            if (!await requestStream.MoveNext(context.CancellationToken))
                return;

            var clientId = _messageProcessor.GetClientId(requestStream.Current);
            Logger.LogInformation($"{clientId} connected");
            var subscriber = new SubscribersModel<TResponse>
            {
                Subscriber = responseStream,
                Id = $"{clientId}"
            };

            //_serverGrpcSubscribers.AddSubscriber(subscriber);

            do
            {
                if (requestStream.Current == null)
                    continue;

                var resultMessage = _messageProcessor.Process(requestStream.Current);
                if (resultMessage == null)
                    continue;

                await _serverGrpcSubscribers.BroadcastMessageAsync(resultMessage);
            } while (await requestStream.MoveNext(context.CancellationToken));

            //_serverGrpcSubscribers.RemoveSubscriber(subscriber);
            Logger.LogInformation($"{clientId} disconnected");
        }

        public Task BroadcastAsync(TResponse resultMessage)
        {
            return _serverGrpcSubscribers.BroadcastMessageAsync(resultMessage);
        }

        public void Dispose()
        {
            Logger.LogInformation("Cleaning up");
        }
    }
}
