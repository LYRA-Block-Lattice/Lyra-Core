using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize.WorkFlow
{
    public interface IWorkFlow
    {
        WorkFlowDescription GetDescription();
        Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last);
        Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send);
        Task<TransactionBlock> ExtraOpsAsync(DagSystem sys, string hash);
    }
}
