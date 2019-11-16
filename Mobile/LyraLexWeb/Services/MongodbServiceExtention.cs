using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LyraLexWeb.Services
{
    public static class MongodbServiceExtention
    {
        public static void AddMongodb(this IServiceCollection services, string databasePath)
        {
            services.AddTransient<MongodbContext, MongodbContext>();
            services.Configure<MongodbConfig>(options => options.DatabasePath = databasePath);
        }
    }
}
