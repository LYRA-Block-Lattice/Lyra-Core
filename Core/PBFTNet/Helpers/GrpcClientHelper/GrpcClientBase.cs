using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;

namespace GrpcClientHelper
{
    public abstract class GrpcClientBase<TRequest, TResponse>
    {
        public CancellationTokenSource Stop { get; }

        public abstract AsyncDuplexStreamingCall<TRequest, TResponse> CreateDuplexClient(GrpcChannel channel);

        public abstract TRequest CreateMessage(string id, string type, byte[] payload);

        public abstract (string id, string type, byte[] payload) MessagePayload { get; }

        protected ILogger _log;

        public GrpcClientBase()
        {
            Stop = new CancellationTokenSource();
            _log = new SimpleLogger("GrpcClientBase").Logger;
        }

        public async Task Do(GrpcChannel channel, Action onConnection = null, Action<TResponse> onMessage = null, Action onShuttingDown = null)
        {
            using (var duplex = CreateDuplexClient(channel))
            {
                onConnection?.Invoke();

                var responseTask = Task.Run(async () =>
                {
                    try
                    {
                        // receive pump
                        while (await duplex.ResponseStream.MoveNext(Stop.Token))
                        {
                            var msg = duplex.ResponseStream.Current;
                            if (onMessage != null)
                                onMessage(msg);
                        }
                    }
                    catch(Exception ex)
                    {
                        _log.LogInformation($"In receive pump: {ex.ToString()}");
                    }
                });

                // send pump
                while (!Stop.Token.IsCancellationRequested)
                {
                    try
                    {
                        var msg = MessagePayload;
                        await duplex.RequestStream.WriteAsync(CreateMessage(msg.id, msg.type, msg.payload));
                    }
                    catch(Exception ex)
                    {
                        _log.LogInformation($"In send pump: {ex.Message}");
                        break;  //shutdown
                    }
                }                   

                await duplex.RequestStream.CompleteAsync();
            }
        }
    }
}


