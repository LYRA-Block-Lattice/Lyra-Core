using Lyra.Authorizer.Decentralize;
using Lyra.Core.API;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LyraNodesBot
{
    public class StreamWatcher// : IAsyncObserver<ChatMsg>
    {
        IClusterClient _client;
        private IAsyncStream<ChatMsg> _gossipStream;

        public StreamWatcher(IClusterClient client)
        {
            _client = client;
        }

        public async Task Init(string IdentityString)
        {
            _gossipStream = _client.GetStreamProvider(LyraGossipConstants.LyraGossipStreamProvider)
                .GetStream<ChatMsg>(Guid.Parse(LyraGossipConstants.LyraGossipStreamId), LyraGossipConstants.LyraGossipStreamNameSpace);
            await _gossipStream.SubscribeAsync(OnNextAsync, OnErrorAsync, OnCompletedAsync);

            await SendMessage(new ChatMsg { From = IdentityString, Text = IdentityString + " Up", Type = ChatMessageType.NewStaker });
        }

        public Task OnCompletedAsync()
        {
            Console.WriteLine("Chatroom message stream received stream completed event");
            return Task.CompletedTask;
        }

        public Task OnErrorAsync(Exception ex)
        {
            Console.WriteLine($"Chatroom is experiencing message delivery failure, ex :{ex}");
            return Task.CompletedTask;
        }

        public Task OnNextAsync(ChatMsg item, StreamSequenceToken token = null)
        {
            var info = $"=={item.Created}==         {item.From} said: {item.Text}";
            Console.WriteLine(info);
            return Task.CompletedTask;
        }

        public async Task SendMessage(ChatMsg msg)
        {
            await _gossipStream.OnNextAsync(msg);
        }
    }
}
