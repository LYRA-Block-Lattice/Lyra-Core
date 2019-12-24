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
using System.Configuration;
using Microsoft.Extensions.Configuration;
using System.IO;
using Lyra.Core.Utils;

namespace LyraNodesBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Wait for Lyra node start. Press enter to continue...");
            await Task.Delay(10000);

            using (var host = CreateHost())
            {
                host.Start();
                var client = (ClusterClientHostedService)host.Services.GetService<IHostedService>();

                var api = client.Client.GetGrain<INodeAPI>(0);
                var height = await api.GetSyncHeight();

                var config = host.Services.GetService<LyraNodeConfig>();
                var monitor = new NodesMonitor();
                monitor.Start();

                var watch = new StreamWatcher(client.Client);
                var myName = "LyraNodeBot";
                watch.OnNodeChat += async (m) => await monitor.SendGroupMessageAsync($"From: {m.From}\nMessage Type: {m.Type}\nText: {m.Text}");
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
                    // build config
                    var Configuration = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", false)
                        .AddEnvironmentVariables()
                        .Build();

                    services.Configure<LyraNodeConfig>(Configuration.GetSection("LyraNode"));

                    services.AddSingleton<ClusterClientHostedService>();
                    services.AddSingleton<IHostedService>(_ => _.GetService<ClusterClientHostedService>());
                    services.AddSingleton(_ => _.GetService<ClusterClientHostedService>().Client);

                    //services.AddHostedService<DAGClientHostedService>();

                    services.Configure<ConsoleLifetimeOptions>(options =>
                    {
                        options.SuppressStatusMessages = true;
                    });
                })
                .Build();
        }
    }
}
