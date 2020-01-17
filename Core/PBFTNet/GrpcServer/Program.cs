using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;

namespace GrpcServer
{
    public class Program
    {
        const int PORT = 19019;

        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>()
                    .ConfigureKestrel(options =>
                    {
                        options.Limits.MinRequestBodyDataRate = null;
                        options.Listen(IPAddress.Any, PORT,
                        listenOptions =>
                        {
                            listenOptions.UseHttps("grpcServer.pfx", "1511");
                            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                        });
                    });
                });
    }
}
