using Lyra.Authorizer;
using Lyra.Authorizer.Decentralize;
using Lyra.Authorizer.Services;
using Lyra.Core.Accounts;
using Lyra.Core.Accounts.Node;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Lyra.Node2
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            OptionsConfigurationServiceCollectionExtensions.Configure<LyraConfig>(services, Configuration.GetSection("Lyra"));

            // mongodb
            services.AddSingleton<IAccountCollection, MongoAccountCollection>();
            services.AddSingleton<IAccountDatabase, MongoServiceAccountDatabase>();
            services.AddSingleton(typeof(ServiceAccount));
            //services.AddSingleton<INotifyAPI, NotifyService>();

            //services.AddGrpc();

            services.AddMvc();
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                //endpoints.MapGrpcService<ApiService>();
                endpoints.MapControllers();
                //endpoints.MapGet("/", async context =>
                //{
                //    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                //});
            });
        }
    }
}
