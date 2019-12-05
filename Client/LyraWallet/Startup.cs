using Lyra.Client.Lib;
using LyraWallet.States;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Xamarin.Essentials;

namespace LyraWallet
{
    public static class Startup
    {
        public static App Init(Action<HostBuilderContext, IServiceCollection> nativeConfigureServices)
        {
            var systemDir = FileSystem.CacheDirectory;
            ExtractSaveResource("LyraWallet.appsettings.json", systemDir);
            var fullConfig = Path.Combine(systemDir, "LyraWallet.appsettings.json");

            var host = new HostBuilder()
                            .ConfigureHostConfiguration(c =>
                            {
                                c.AddCommandLine(new string[] { $"ContentRoot={FileSystem.AppDataDirectory}" });
                                c.AddJsonFile(fullConfig);
                            })
                            .ConfigureServices((c, x) =>
                            {
                                nativeConfigureServices(c, x);
                                ConfigureServices(c, x);
                            })
                            .ConfigureServices(services =>
                            {
                                services.AddSingleton<ClusterClientHostedService>();
                                services.AddSingleton<IHostedService>(_ => _.GetService<ClusterClientHostedService>());
                                services.AddSingleton(_ => _.GetService<ClusterClientHostedService>().Client);

                                services.AddHostedService<DAGClientHostedService>();

                                services.Configure<ConsoleLifetimeOptions>(options =>
                                {
                                    options.SuppressStatusMessages = true;
                                });
                            })
                            .ConfigureLogging(l => l.AddConsole(o =>
                            {
                                o.DisableColors = true;
                            }))
                            .Build();

            App.ServiceProvider = host.Services;

            return App.ServiceProvider.GetService<App>();
        }


        static void ConfigureServices(HostBuilderContext ctx, IServiceCollection services)
        {
            if (ctx.HostingEnvironment.IsDevelopment())
            {
                var world = ctx.Configuration["Hello"];
            }

            services.AddHttpClient();
            //services.AddTransient<IMainViewModel, MainViewModel>();
            //services.AddTransient<MainPage>();
            //services.AddSingleton<App>();
        }

        public static void ExtractSaveResource(string filename, string location)
        {
            var a = Assembly.GetExecutingAssembly();
            using (var resFilestream = a.GetManifestResourceStream(filename))
            {
                if (resFilestream != null)
                {
                    var full = Path.Combine(location, filename);

                    using (var stream = File.Create(full))
                    {
                        resFilestream.CopyTo(stream);
                    }

                }
            }
        }
    }
}
