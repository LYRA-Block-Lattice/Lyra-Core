using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LyraLexWeb.Services
{
    public class LyraDealEngine : BackgroundService
    {
        private readonly ILogger<LyraDealEngine> _logger;
        public LyraDealEngine(ILogger<LyraDealEngine> logger)
        {
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while(!stoppingToken.IsCancellationRequested)
            {
                _logger.LogCritical("Lyra Deal Engine: Trade, deal, make, take");
                await Task.Delay(10000);
            }
        }

        
    }
}
