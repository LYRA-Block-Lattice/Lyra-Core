using Lyra.Core.Accounts;
using Lyra.Core.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lyra.Core.Decentralize;
using Microsoft.AspNetCore.Http;
using Lyra.Core.API;
using System;
using System.IO;
using Lyra.Node2.Services;
using Lyra.Shared;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Lyra.Data.Utils;
using Lyra.Data;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Hosting.Server;
using Noded.Services;
using Microsoft.AspNetCore.Diagnostics;
using Newtonsoft.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.Extensions.FileProviders;
using WorkflowCore.Interface;
using Lyra.Core.WorkFlow;
using Lyra.Core.Blocks;
using System.Linq;
using WorkflowCore.Services;
using System.Reflection;
using MongoDB.Bson.Serialization;

namespace Lyra.Node2
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        private IWebHostEnvironment _env;

        public static IApplicationBuilder App { get; private set; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // lyra network ID must be set early
            var networkId = Environment.GetEnvironmentVariable($"{LyraGlobal.OFFICIALDOMAIN.ToUpper()}_NETWORK");
            if (networkId == null)
                networkId = "devnet";   // for dev convenient

            LyraNodeConfig.Init(networkId);

            // the apis
            services.AddScoped<INodeAPI, NodeAPI>();
            services.AddScoped<INodeTransactionAPI, ApiService>();
            services.AddSingleton<IHostEnv, HostEnvService>();
            services.AddSingleton<IAccountCollectionAsync, MongoAccountCollection>();

            services.AddMvc();
            services.AddControllers();
            services.AddSignalR();

            // workflow need this
            BsonClassMap.RegisterClassMap<LyraContext>(cm =>
            {
                cm.AutoMap();
                cm.SetIsRootClass(false);
            });

            //services.AddGrpc();
            services.AddWorkflow(cfg =>
            {
                // lyra network ID must be set early
                var networkId = Environment.GetEnvironmentVariable($"{LyraGlobal.OFFICIALDOMAIN.ToUpper()}_NETWORK");
                if (networkId == null)
                    networkId = "devnet";   // for dev convenient

                cfg.UseMongoDB(Neo.Settings.Default.LyraNode.Lyra.Database.DBConnect, "Workflow_" + networkId);
                //cfg.UseSqlite($"Data Source={fn};", true);
                cfg.UsePollInterval(new TimeSpan(0, 0, 0, 2));
                //cfg.UseElasticsearch(new ConnectionSettings(new Uri("http://elastic:9200")), "workflows");
            });

            services.AddTransient<Repeator>();
            services.AddTransient<ReqViewChange>();
            services.AddTransient<CustomMessage>();

            services.AddApiVersioning(options => {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            });

            services.AddVersionedApiExplorer(options =>
            {
                // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                // note: the specified format code will format the version as "'v'major[.minor][-status]"
                options.GroupNameFormat = "'v'VVV";

                // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                // can also be used to control the format of the API version in route templates
                options.SubstituteApiVersionInUrl = true;
            });
            //services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
            //services.AddSwaggerGen(options => options.OperationFilter<SwaggerDefaultValues>());

            // Register the Swagger generator, defining 1 or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = $"{LyraGlobal.PRODUCTNAME} API", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            App = app;
            _env = env;
            //app.UseCors("my");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
                //app.UseExceptionHandler(errorApp =>
                //{
                //    errorApp.Run(async context =>
                //    {
                //        var error = context.Features.Get<IExceptionHandlerFeature>();
                //        if (error != null && error.Error.Message == "System Not Ready.")
                //        {
                //            context.Response.StatusCode = 500; // or another Status accordingly to Exception Type
                //            context.Response.ContentType = "application/json";

                //            var ex = error.Error;
                //            var result = new APIResult()
                //            {
                //                ResultCode = Core.Blocks.APIResultCodes.SystemNotReadyToServe
                //            };
                //            var str = JsonConvert.SerializeObject(result);
                //            await context.Response.WriteAsync(str, Encoding.UTF8);
                //        }
                //        else
                //        {
                //            context.Response.StatusCode = 500; // or another Status accordingly to Exception Type
                //            context.Response.ContentType = "text/html";
                //            await context.Response.WriteAsync("<html>Internal Error</html>", Encoding.UTF8);
                //        }
                //    });
                //});
                //app.UseHsts();
            }

            app.UseCors(builder =>
                builder
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod()
                );

            var logPath = $"{Utilities.GetLyraDataDir(Neo.Settings.Default.LyraNode.Lyra.NetworkId, LyraGlobal.OFFICIALDOMAIN)}/logs/";
            loggerFactory.AddFile(logPath + "noded-{Date}.txt");

            SimpleLogger.Factory = loggerFactory;

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto
            });

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{LyraGlobal.PRODUCTNAME} API V1");
            });

            //app.UseHttpsRedirection();

            // need to host database snapshot
            //app.UseDefaultFiles();
            //app.UseStaticFiles(new StaticFileOptions()
            //{
            //    ServeUnknownFileTypes = true,

            //    FileProvider = new PhysicalFileProvider(
            //         Path.Combine(env.ContentRootPath, "webroot")),
            //    RequestPath = "/db",

            //    OnPrepareResponse = context =>
            //    {
            //        context.Context.Response.Headers["Content-Disposition"] = "attachment";
            //    }
            //});

            var host = app.ApplicationServices.GetService<IWorkflowHost>();

            // register work flows
            var alltypes = typeof(DebiWorkflow)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(DebiWorkflow)) && !t.IsAbstract);

            foreach (var type in alltypes)
            {
                var methodInfo = typeof(WorkflowHost).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(a => a.Name == "RegisterWorkflow")
                    .Last();

                var genericMethodInfo = methodInfo.MakeGenericMethod(type, typeof(LyraContext));

                genericMethodInfo.Invoke(host, new object[] { });
            }

            //host.Start(); // will start later on consensus network is ready
            var evn = app.ApplicationServices.GetService<IHostEnv>();
            evn.SetWorkflowHost(host);

            app.UseRouting();
            app.UseCors();
            app.UseAuthorization();
            app.UseWebSockets();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<LyraEventHub>("/events");
                endpoints.MapControllers();
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });
            });
        }
    }
}
