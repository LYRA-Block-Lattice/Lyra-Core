using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Lyra.Node2
{
    public class LyraNodeService : Lyra.Node2.Protos.LyraApi.LyraApiBase
    {
        private readonly ILogger<LyraNodeService> _logger;
        public LyraNodeService(ILogger<LyraNodeService> logger)
        {
            _logger = logger;
        }

        
    }
}
