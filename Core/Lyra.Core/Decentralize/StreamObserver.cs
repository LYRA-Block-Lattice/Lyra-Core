using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Lyra.Core.Decentralize
{
    //public class StreamObserver : IAsyncObserver<ChatMsg>
    //{
    //    private ILogger logger;
    //    public StreamObserver(ILogger logger)
    //    {
    //        this.logger = logger;
    //    }

    //    public Task OnCompletedAsync()
    //    {
    //        this.logger.LogInformation("Chatroom message stream received stream completed event");
    //        return Task.CompletedTask;
    //    }

    //    public Task OnErrorAsync(Exception ex)
    //    {
    //        this.logger.LogInformation($"Chatroom is experiencing message delivery failure, ex :{ex}");
    //        return Task.CompletedTask;
    //    }

    //    public Task OnNextAsync(ChatMsg item, StreamSequenceToken token = null)
    //    {
    //        var info = $"=={item.Created}==         {item.From} said: {item.Text}";
    //        this.logger.LogInformation(info);
    //        Console.WriteLine(info);
    //        return Task.CompletedTask;
    //    }
    //}
}
