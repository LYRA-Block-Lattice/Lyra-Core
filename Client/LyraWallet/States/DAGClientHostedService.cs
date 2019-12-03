using Lyra.Core.API;
using Microsoft.Extensions.Hosting;
using Orleans;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace LyraWallet.States
{
    public class DAGClientHostedService : IHostedService
    {
        private readonly IClusterClient _client;

        public DAGClientHostedService(IClusterClient client)
        {
            _client = client;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var node = _client.GetGrain<IDAGNode>(0);
            var result = await node.GetVersion(3, DeviceInfo.Name, DeviceInfo.VersionString);
            Trace.Assert(result.ResultCode == Lyra.Core.Protos.APIResultCodes.Success);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
