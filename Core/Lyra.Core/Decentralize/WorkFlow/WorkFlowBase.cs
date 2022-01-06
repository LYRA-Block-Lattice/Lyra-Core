using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize.WorkFlow
{
    public class BlockDesc
    {
        public BlockTypes BlockType { get; set; }
        //public Type AuthorizerType { get; set; }
        public Type TheBlock { get; set; }
    }
    public class WorkFlowDescription
    {
        public string Action { get; set; }
        public BrokerRecvType RecvVia { get; set; }
        public BlockDesc[] Blocks { get; set; }
    }

    public abstract class WorkFlowBase : IWorkFlow
    {
        public abstract WorkFlowDescription GetDescription();
        public abstract Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send);
        public virtual Task<TransactionBlock> ExtraOpsAsync(DagSystem sys, string hash)
        {
            return Task.FromResult((TransactionBlock)null);
        }
        public abstract Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last);
    }
}
