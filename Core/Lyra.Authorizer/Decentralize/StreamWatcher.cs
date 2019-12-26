using Lyra.Authorizer.Decentralize;
using Lyra.Core.API;
using Lyra.Core.Cryptography;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Authorizer.Decentralize
{
    public delegate void NodeMessageHandler(SourceSignedMessage msg);
    public class StreamWatcher
    {
        protected IClusterClient _client;
        private IAsyncStream<SourceSignedMessage> _gossipStream;

        public event NodeMessageHandler OnNodeChat;

        public StreamWatcher(IClusterClient client)
        {
            _client = client;
        }

        public virtual async Task Init(string IdentityString)
        {
            _gossipStream = _client.GetStreamProvider(LyraGossipConstants.LyraGossipStreamProvider)
                .GetStream<SourceSignedMessage>(Guid.Parse(LyraGossipConstants.LyraGossipStreamId), LyraGossipConstants.LyraGossipStreamNameSpace);
            await _gossipStream.SubscribeAsync(OnNextAsync, OnErrorAsync, OnCompletedAsync);
        }

        public virtual Task OnCompletedAsync()
        {
            Console.WriteLine("Chatroom message stream received stream completed event");
            return Task.CompletedTask;
        }

        public virtual Task OnErrorAsync(Exception ex)
        {
            Console.WriteLine($"Chatroom is experiencing message delivery failure, ex :{ex}");
            return Task.CompletedTask;
        }

        public virtual Task OnNextAsync(SourceSignedMessage msg, StreamSequenceToken token = null)
        {
            var item = msg as ChatMsg;
            if (item != null)
            {
                var info = $"=={item.Created}==         {item.From} said: {item.Text}";
                Console.WriteLine(info);

                OnNodeChat?.Invoke(item);
            }
            return Task.CompletedTask;
        }

        public virtual async Task SendMessage(ChatMsg msg)
        {
            var signr = new SignaturesBase();

            await _gossipStream.OnNextAsync(msg);
        }
    }
}
