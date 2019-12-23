using Lyra.Core.API;
using Lyra.Core.Utils;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lyra.Authorizer.Decentralize
{
    //public class DAGNode
    //{
    //    public static async Task<IClusterClient> ConnectClient()
    //    {
    //        IClusterClient client;
    //        client = new ClientBuilder()
    //            .UseZooKeeperClustering((options) =>
    //            {
    //                options.ConnectionString = OrleansSettings.AppSetting["ZooKeeperClusteringSilo:ConnectionString"];
    //            })
    //            .Configure<ClusterOptions>(options =>
    //            {
    //                options.ClusterId = OrleansSettings.AppSetting["Cluster:ClusterId"];
    //                options.ServiceId = OrleansSettings.AppSetting["Cluster:ServiceId"];
    //            })
    //            .Build();

    //        await client.Connect();
    //        Console.WriteLine("Client successfully connected to silo host \n");
    //        return client;
    //    }
    //}
}
