using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Node2.Services
{
    public class NodeService : BackgroundService
    {
        public NodeService()
        {
            
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            

            while (!stoppingToken.IsCancellationRequested)
            {
                // do work
                //serviceAccount.TimingSync();

                //_logger.LogCritical("Lyra Deal Engine: Trade, deal, make, take");
                await Task.Delay(10 * 60 * 1000);
            }
        }


    }
}
