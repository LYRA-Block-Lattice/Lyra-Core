using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GrpcServerHelper
{
    public class ServerGrpcSubscribersBase<TResponse>
    {
        private readonly ConcurrentDictionary<string, SubscribersModel<TResponse>> Subscribers = 
            new ConcurrentDictionary<string, SubscribersModel<TResponse>>();

        private ILogger Logger { get; set; }

        public ServerGrpcSubscribersBase(ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<ServerGrpcSubscribersBase<TResponse>> ();
        }

        public async Task BroadcastMessageAsync(TResponse message)
        {
            await BroadcastMessages(message);
        }

        public void AddSubscriber(SubscribersModel<TResponse> subscriber)
        {
            bool added = Subscribers.TryAdd(subscriber.Id, subscriber);
            Logger.LogInformation($"New subscriber added: {subscriber.Id}");
            if (!added)
                Logger.LogInformation($"could not add subscriber: {subscriber.Id}");
        }

        public void RemoveSubscriber(SubscribersModel<TResponse> subscriber)
        {
            try
            {
                Subscribers.TryRemove(subscriber.Id, out SubscribersModel<TResponse> item);
                Logger.LogInformation($"Force Remove: {item.Id} - no longer works");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Could not remove {subscriber.Id}");
            }
        }

        public virtual bool Filter(SubscribersModel<TResponse> subscriber, TResponse message) => true;
        
        private async Task BroadcastMessages(TResponse message)
        {
            foreach (var subscriber in Subscribers.Values)
            {
                var item = await SendMessageToSubscriber(subscriber, message);
                //if (item != null)
                //    RemoveSubscriber(item);
            }
        }

        private async Task<SubscribersModel<TResponse>> SendMessageToSubscriber(SubscribersModel<TResponse> subscriber, TResponse message)
        {
            try
            {
                if (Filter(subscriber, message))
                {
                    Logger.LogInformation($"Sending TResponse: {message}");
                    await subscriber.Subscriber.WriteAsync(message);
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not send");
                return subscriber;
            }
        }
    }
}
