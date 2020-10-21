using System.Net;
using System.Threading.Tasks;
using Lyra.Core.API;
using Lyra.Node2.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.IO;
using System;
using System.Threading;
using Lyra.Core.Utils;
using System.Diagnostics;
using Lyra.Core.Decentralize;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Connections;

namespace Lyra.Node2
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            if(args.Length > 0 && args[0] == "/debug")
            {
                Console.WriteLine("Waiting for debugger to attach");
                while (!Debugger.IsAttached)
                {
                    await Task.Delay(200);
                }
                Console.WriteLine("Debugger attached");
            }

            using (var host = CreateHostBuilder(args).Build())
            {
                host.Run();
            }
        }

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSystemd()   // https://swimburger.net/blog/dotnet/how-to-run-aspnet-core-as-a-service-on-linux
                .UseWindowsService()  // https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-3.1&tabs=visual-studio
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureServices(services =>
                {
                    services.AddHostedService<NodeService>();
                });
    }
}
