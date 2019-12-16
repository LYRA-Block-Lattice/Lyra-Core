using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Lyra.Authorizer.Decentralize;
using Lyra.Core.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime;

namespace Lyra.Client.Lib
{
    public class ClusterClientHostedService : IHostedService
    {
        //private readonly ILogger<ClusterClientHostedService> _logger;

        public ClusterClientHostedService(/*ILogger<ClusterClientHostedService> logger, ILoggerProvider loggerProvider*/)
        {
            //_logger = logger;
            Client = new ClientBuilder()
                .UseZooKeeperClustering((options) =>
                {
                    options.ConnectionString = OrleansSettings.AppSetting["ZooKeeperClusteringSilo:ConnectionString"];
                })
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = OrleansSettings.AppSetting["Cluster:ClusterId"];
                    options.ServiceId = OrleansSettings.AppSetting["Cluster:ServiceId"];
                })
                .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(ILyraGossip).Assembly).WithReferences())
                .AddSimpleMessageStreamProvider(GossipConstants.LyraGossipStreamProvider)
                //.Configure<ClusterOptions>(options =>
                //{
                //    options.ClusterId = "dev";
                //    options.ServiceId = "LyraAuthorizerNode";
                //})
                //.UseLocalhostClustering()
                //.UseStaticClustering(new IPEndPoint(IPAddress.Parse("192.168.3.91"), 30000))
                //.ConfigureLogging(builder => builder.AddProvider(loggerProvider))
                .Build();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var attempt = 0;
            var maxAttempts = 100;
            var delay = TimeSpan.FromSeconds(1);
            return Client.Connect(async error =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                if (++attempt < maxAttempts)
                {
                    //_logger.LogWarning(error,
                    //    "Failed to connect to Orleans cluster on attempt {@Attempt} of {@MaxAttempts}.",
                    //    attempt, maxAttempts);

                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }

                    return true;
                }
                else
                {
                    //_logger.LogError(error,
                    //    "Failed to connect to Orleans cluster on attempt {@Attempt} of {@MaxAttempts}.",
                    //    attempt, maxAttempts);

                    return false;
                }
            });
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Client.Close();
            }
            catch (OrleansException error)
            {
                //_logger.LogWarning(error, "Error while gracefully disconnecting from Orleans cluster. Will ignore and continue to shutdown.");
            }
        }

        public IClusterClient Client { get; }
    }
}
