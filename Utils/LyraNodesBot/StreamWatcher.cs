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
    public class StreamWatcher
    {
        IClusterClient _client;
        private IAsyncStream<ChatMsg> _gossipStream;

        //private INodeAPI _gsp;

        public StreamWatcher(IClusterClient client)
        {
            _client = client;
        }

        public async Task Init(string IdentityString)
        {
            //_gsp = _client.GetGrain<INodeAPI>(0);

            // gossip channel
            //var room = _client.GetGrain<ILyraGossip>(LyraGossipConstants.LyraGossipStreamId);
            //var gossipStreamId = await room.Join(IdentityString);
            _gossipStream = _client.GetStreamProvider(LyraGossipConstants.LyraGossipStreamProvider)
                .GetStream<ChatMsg>(Guid.Parse(LyraGossipConstants.LyraGossipStreamId), LyraGossipConstants.LyraGossipStreamNameSpace);
            await _gossipStream.SubscribeAsync<ChatMsg>(async (data, token) =>
            {
                Console.WriteLine(data);
            });

            //await SendMessage(new ChatMsg { From = IdentityString, Text = IdentityString + " Up", Type = ChatMessageType.NewStaker });
        }


        public async Task SendMessage(ChatMsg msg)
        {
            //await _gsp.Message(msg);
            await _gossipStream.OnNextAsync(msg);
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
            Console.WriteLine(info);
            return Task.CompletedTask;
        }

        public Task<Guid> Join(string nickname)
        {
            throw new NotImplementedException();
        }

        public Task<Guid> Leave(string nickname)
        {
            throw new NotImplementedException();
        }

        public Task<bool> Message(ChatMsg msg)
        {
            throw new NotImplementedException();
        }

        public Task<string[]> GetMembers()
        {
            throw new NotImplementedException();
        }
    }
}
