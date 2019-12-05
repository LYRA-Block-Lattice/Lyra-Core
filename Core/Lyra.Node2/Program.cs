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

namespace Lyra.Node2
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
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
                    .UseAdoNetClustering(options =>
                    {
                        options.Invariant = "System.Data.SqlClient";
                        options.ConnectionString = "Data Source=ZION;Initial Catalog=Orleans;Persist Security Info=True;User ID=orleans;Password=orleans";
                    })
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = "dev";
                        options.ServiceId = "LyraAuthorizerNode";
                    })
                    .Configure<EndpointOptions>(options => options.AdvertisedIPAddress = IPAddress.Parse("192.168.3.91"))
                    .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(ApiService).Assembly).WithReferences())
                    .AddAdoNetGrainStorage("OrleansStorage", options =>
                    {
                        options.Invariant = "System.Data.SqlClient";
                        options.ConnectionString = "Data Source=ZION;Initial Catalog=Orleans;Persist Security Info=True;User ID=orleans;Password=orleans";
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
