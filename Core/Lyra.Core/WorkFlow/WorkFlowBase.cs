using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Lyra.Core.WorkFlow
{
    public class WorkFlowDescription
    {
        public string Action { get; set; }
        public BrokerRecvType RecvVia { get; set; }
        public Func<DagSystem, SendTransferBlock, Task<TransactionBlock>>[] Steps { get; set; }
    }

    public abstract class WorkFlowBase : IDebiWorkFlow
    {
        public abstract WorkFlowDescription GetDescription();
        public virtual Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
        {
            return OneByOneAsync(sys, send, GetDescription().Steps);
        }
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
