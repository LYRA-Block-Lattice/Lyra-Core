using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkflowCore.Interface;

namespace Lyra.Core.WorkFlow
{
    public interface IDebiWorkFlow
    {
        WorkFlowDescription GetDescription();
        Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last);
        Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send);
        Task<TransactionBlock> ExtraOpsAsync(DagSystem sys, string hash);
    }
}
