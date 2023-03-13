using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Lyra.Core.WorkFlow.Uni
{
    public class UniSharedWFCode : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            throw new Exception("Shared code. Should not call me.");
        }

        public override Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            throw new Exception("Shared code. Should not call me.");
        }
    }
}
