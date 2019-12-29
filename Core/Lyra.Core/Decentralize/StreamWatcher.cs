using Lyra.Core.Decentralize;
using Lyra.Core.API;
using Lyra.Core.Cryptography;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public delegate void NodeMessageHandler(SourceSignedMessage msg);
    public class StreamWatcher
    {
        public event NodeMessageHandler OnNodeChat;

        public StreamWatcher()
        {
        }

        public virtual async Task Init(string IdentityString)
        {
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

        public virtual Task OnNextAsync(SourceSignedMessage msg)
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

        }
    }
}
