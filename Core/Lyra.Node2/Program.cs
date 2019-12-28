using System.Net;
using System.Threading.Tasks;
using Lyra.Authorizer.Decentralize;
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
using Lyra.Authorizer.Services;

namespace Lyra.Node2
{
    public class Program
    {
        static CancellationTokenSource _cancel;
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
                _cancel = new CancellationTokenSource();
                await host.StartAsync();
                Console.ReadLine();
            }
        }

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
