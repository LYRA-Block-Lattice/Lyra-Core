using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public class ConsensusEngagingWorker : ConsensusWorker
    {
        public ConsensusEngagingWorker(ConsensusService context, string hash) : base(context, hash)
        {
        }

        protected override Task AuthorizeAsync(AuthorizingMsg msg)
        {
            _log.LogWarning("Engaging Sync Mode. Bypass authorizing.");
            return Task.CompletedTask;
        }
    }
}
