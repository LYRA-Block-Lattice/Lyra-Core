using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow
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

        protected async Task<TransactionBlock> OneByOneAsync(DagSystem sys, SendTransferBlock send,
            params Func<DagSystem, SendTransferBlock, Task<TransactionBlock>>[] operations)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var desc = GetDescription();

            var index = blocks.Count - (desc.RecvVia == BrokerRecvType.None ? 0 : 1);
            if (index >= operations.Length)
                return null;

            return await operations[index](sys, send);
        }
    }
}
