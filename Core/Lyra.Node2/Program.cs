using System.Net;
using System.Threading.Tasks;
using Lyra.Authorizer.Decentralize;
using Lyra.Core.API;
using Lyra.Node2.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans;
using Microsoft.Extensions.Configuration;
using System.IO;
using System;
using System.Threading;

namespace Lyra.Node2
{
    public class Program
    {
        static CancellationTokenSource _cancel;
        public static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            var host = CreateHostBuilder(args).Build();
            _cancel = new CancellationTokenSource();
            await host.RunAsync(_cancel.Token);
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            _cancel.Cancel();
        }

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .UseOrleans((cntx, siloBuilder) =>
                {
                    siloBuilder
                    //.UseLocalhostClustering()
                    //.UseAdoNetClustering(options =>
                    //{
                    //    options.Invariant = "System.Data.SqlClient";
                    //    options.ConnectionString = "Data Source=ZION;Initial Catalog=Orleans;Persist Security Info=True;User ID=orleans;Password=orleans";
                    //})
                    .UseZooKeeperClustering((options) =>
                    {
                        options.ConnectionString = OrleansSettings.AppSetting["ZooKeeperClusteringSilo:ConnectionString"];
                    })
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = OrleansSettings.AppSetting["Cluster:ClusterId"];
                        options.ServiceId = OrleansSettings.AppSetting["Cluster:ServiceId"];
                    })
                    .Configure<EndpointOptions>(options => options.AdvertisedIPAddress = IPAddress.Parse(OrleansSettings.AppSetting["EndPoint:AdvertisedIPAddress"]))
                    .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(ApiService).Assembly).WithReferences())
                    //.AddAdoNetGrainStorage("OrleansStorage", options =>
                    //{
                    //    options.Invariant = "System.Data.SqlClient";
                    //    options.ConnectionString = "Data Source=ZION;Initial Catalog=Orleans;Persist Security Info=True;User ID=orleans;Password=orleans";
                    //})
                    .UseDashboard(options => {
                        options.Port = 8080;
                    })
                    .AddStartupTask((sp, token) =>
                    {
                        INodeAPI localNode = sp.GetRequiredService<INodeAPI>();

                        return Task.CompletedTask;
                    });
                })
                .ConfigureServices(services =>
                {
                    services.AddHostedService<NodeService>();
                    services.AddSingleton<INodeAPI, ApiService>();
                });
    }
}
