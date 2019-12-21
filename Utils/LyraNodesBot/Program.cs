using Lyra.Authorizer.Decentralize;
using Lyra.Client.Lib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using Orleans;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans.Streams;
using Lyra.Core.API;

namespace LyraNodesBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var monitor = new NodesMonitor();
            monitor.Start();

            Console.WriteLine("Wait for Lyra node start. Press enter to continue...");
            await Task.Delay(15000);

            using (var host = CreateHost())
            {
                host.Start();
                var client = (ClusterClientHostedService)host.Services.GetService<IHostedService>();

                var api = client.Client.GetGrain<INodeAPI>(0);
                var height = await api.GetSyncHeight();

                var watch = new StreamWatcher(client.Client);
                var myName = "LyraNodeBot";
                watch.OnNodeChat += async (m) => await monitor.SendGroupMessageAsync($"{m.From}: {m.Text}");
                await watch.Init(myName);

                while (true)
                {
                    var line = Console.ReadLine();
                    if (line?.Trim() == "quit")
                        break;

                    await watch.SendMessage(new ChatMsg(myName, line));
                }

                monitor.Stop();
            }
        }

        //private static async Task AttachStream(IClusterClient client)
        //{
        //    var room = client.GetGrain<ILyraGossip>(GossipConstants.LyraGossipStreamId);
        //    var streamId = await stream.OnNextAsync(new ChatMsg("System", $"{nickname} joins the chat '{this.GetPrimaryKeyString()}' ..."));
        //    var stream = client.GetStreamProvider(GossipConstants.LyraGossipStreamProvider)
        //        .GetStream<ChatMsg>(streamId, GossipConstants.LyraGossipStreamNameSpace);
        //    //subscribe to the stream to receiver furthur messages sent to the chatroom
        //    await stream.SubscribeAsync(new StreamObserver(client.ServiceProvider.GetService<ILoggerFactory>()
        //        .CreateLogger($"{joinedChannel} channel")));
        //}

        private static IHost CreateHost()
        {
            return new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ClusterClientHostedService>();
                    services.AddSingleton<IHostedService>(_ => _.GetService<ClusterClientHostedService>());
                    services.AddSingleton(_ => _.GetService<ClusterClientHostedService>().Client);

                    //services.AddHostedService<DAGClientHostedService>();

                    services.Configure<ConsoleLifetimeOptions>(options =>
                    {
                        options.SuppressStatusMessages = true;
                    });
                })
                //.ConfigureLogging(builder =>
                //{
                //    builder.AddConsole();
                //})
                .Build();
        }
    }
}
