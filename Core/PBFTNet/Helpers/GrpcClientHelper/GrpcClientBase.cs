using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace GrpcClientHelper
{
    public abstract class GrpcClientBase<TRequest, TResponse>
    {
        public abstract AsyncDuplexStreamingCall<TRequest, TResponse> CreateDuplexClient(Channel channel);

        public abstract TRequest CreateMessage(object ob);

        public abstract string MessagePayload { get; }

        public async Task Do(Channel channel, Action onConnection = null, Action onShuttingDown = null)
        {
            using (var duplex = CreateDuplexClient(channel))
            {
                onConnection?.Invoke();

                var responseTask = Task.Run(async () =>
                {
                    while (await duplex.ResponseStream.MoveNext(CancellationToken.None))
                        Console.WriteLine($"{duplex.ResponseStream.Current}");
                });

                string payload;
                while (!string.IsNullOrEmpty(payload = MessagePayload))
                    await duplex.RequestStream.WriteAsync(CreateMessage(payload));

                await duplex.RequestStream.CompleteAsync();
            }

            onShuttingDown?.Invoke();
            await channel.ShutdownAsync();
        }
    }
}


