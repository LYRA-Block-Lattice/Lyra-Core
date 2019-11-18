using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LyraLexWeb.Services
{
    public static class LyraRpcServiceExtention
    {
        public static void AddLyraRpc(this IServiceCollection services, string nodeAddress, int rpcPort)
        {
            services.AddTransient<LyraRpcContext, LyraRpcContext>();
            services.Configure<LyraRpcConfig>(options =>
            {
                options.nodeHost = nodeAddress;
                options.nodePort = rpcPort;
            });
        }
    }
}
