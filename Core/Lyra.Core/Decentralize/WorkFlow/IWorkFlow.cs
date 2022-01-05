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
        Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last);
        BrokerRecvType GetRecvType();
        Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send);
        Task<TransactionBlock> ExtraOpsAsync(DagSystem sys, string hash);
    }
}
