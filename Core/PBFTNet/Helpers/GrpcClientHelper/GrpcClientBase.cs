using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;

namespace GrpcClientHelper
{
    public abstract class GrpcClientBase<TRequest, TResponse>
    {
        public abstract AsyncDuplexStreamingCall<TRequest, TResponse> CreateDuplexClient(GrpcChannel channel);

        public abstract TRequest CreateMessage(object ob);

        public abstract string MessagePayload { get; }

        public async Task Do(GrpcChannel channel, Action onConnection = null, Action<TResponse> onMessage = null, Action onShuttingDown = null)
        {
            using (var duplex = CreateDuplexClient(channel))
            {
                onConnection?.Invoke();

                var responseTask = Task.Run(async () =>
                {
                    // receive pump
                    while (await duplex.ResponseStream.MoveNext(CancellationToken.None))
                    {
                        var msg = duplex.ResponseStream.Current;
                        Console.WriteLine($"{msg}");
                        if(onMessage != null)
                            onMessage(msg);
                    }                        
                });

                // send pump
                string payload;
                while (!string.IsNullOrEmpty(payload = MessagePayload))
                {
                    try
                    {
                        await duplex.RequestStream.WriteAsync(CreateMessage(payload));
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        break;  //shutdown
                    }
                }                   

                await duplex.RequestStream.CompleteAsync();
            }

            onShuttingDown?.Invoke();
            //await channel.ShutdownAsync();
            channel.Dispose();
        }
    }
}


