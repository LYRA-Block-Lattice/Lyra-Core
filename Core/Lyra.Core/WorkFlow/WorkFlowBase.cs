using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
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

        // use steps to avoid checking block exists every time.
        public Func<DagSystem, SendTransferBlock, Task<TransactionBlock>>[] Steps { get; set; }
    }

    public abstract class WorkFlowBase : DebiWorkflow, IDebiWorkFlow, IWorkflow<LyraContext>
    {
        // IWorkflow<LyraContext>
        public string Id => GetDescription().Action;
        public int Version => 1;

        // IDebiWorkflow
        public abstract WorkFlowDescription GetDescription();
        public virtual async Task<TransactionBlock> MainProcAsync(DagSystem sys, SendTransferBlock send, LyraContext context)
        {
            return await BrokerOpsAsync(sys, send) ?? await ExtraOpsAsync(sys, send.Hash);
        }
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

            var cnt = desc.RecvVia == BrokerRecvType.None || desc.RecvVia == BrokerRecvType.PFRecv;
            var index = blocks.Count - (cnt ? 0 : 1);
            if (index >= operations.Length)
                return null;

            return await operations[index](sys, send);
        }

        protected async Task<TransactionBlock> TransactionOperateAsync(
            DagSystem sys,
            string relatedHash,
            Block prevBlock,
            Func<TransactionBlock> GenBlock,
            Action<TransactionBlock> ChangeBlock
            )
        {
            var lsb = await sys.Storage.GetLastServiceBlockAsync();

            var nextblock = GenBlock();

            // block
            nextblock.ServiceHash = lsb.Hash;
            nextblock.Tags?.Clear();
            nextblock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // ibroker
            (nextblock as IBrokerAccount).RelatedTx = relatedHash;

            if (ChangeBlock != null)
                ChangeBlock(nextblock);

            await nextblock.InitializeBlockAsync(prevBlock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));

            return nextblock;
        }
    }
}
