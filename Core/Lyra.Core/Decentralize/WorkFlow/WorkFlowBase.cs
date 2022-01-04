using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize.WorkFlow
{
    public class WorkFlowBase
    {
        protected static async Task<bool> CheckTokenAsync(DagSystem sys, string tokenName)
        {
            var tokn = await sys.Storage.FindTokenGenesisBlockAsync(null, tokenName);
            return tokn != null;
        }
    }
}
