using Lyra.Core.API;
using Microsoft.Extensions.Hosting;
using Orleans;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Client.Lib
{
    public class DAGClientHostedService : IHostedService
    {
        private readonly IClusterClient _client;

        public INodeAPI Node { get; set; }

        public DAGClientHostedService(IClusterClient client)
        {
            _client = client;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Node = _client.GetGrain<INodeAPI>(0);
            var result = await Node.GetVersion(3, "client", "1.0a");
            Trace.Assert(result.ResultCode == Lyra.Core.Protos.APIResultCodes.Success);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
