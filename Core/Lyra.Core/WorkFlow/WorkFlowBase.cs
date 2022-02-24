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

            var cnt = desc.RecvVia == BrokerRecvType.None || desc.RecvVia == BrokerRecvType.PFRecv;
            var index = blocks.Count - (cnt ? 0 : 1);
            if (index >= operations.Length)
                return null;

            return await operations[index](sys, send);
        }

        protected async Task<TransactionBlock> TransactionOperateAsync(
            DagSystem sys,
            SendTransferBlock sendBlock,
            Func<TransactionBlock> GenBlock,
            Action<TransactionBlock> ChangeBlock
            )
        {
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            var lsb = await sys.Storage.GetLastServiceBlockAsync();

            var nextblock = GenBlock();

            // block
            nextblock.ServiceHash = lsb.Hash;

            // transaction
            nextblock.AccountID = sendBlock.DestinationAccountId;
            nextblock.Balances = new Dictionary<string, long>();
            nextblock.Fee = 0;
            nextblock.FeeCode = LyraGlobal.OFFICIALTICKERCODE;
            nextblock.FeeType = AuthorizationFeeTypes.NoFee;

            // broker
            (nextblock as IBrokerAccount).Name = ((IBrokerAccount)lastblock).Name;
            (nextblock as IBrokerAccount).OwnerAccountId = ((IBrokerAccount)lastblock).OwnerAccountId;
            (nextblock as IBrokerAccount).RelatedTx = sendBlock.Hash;

            nextblock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            var latestBalances = lastblock.Balances.ToDecimalDict();
            var recvBalances = lastblock.Balances.ToDecimalDict();
            nextblock.Balances = recvBalances.ToLongDict();

            if (ChangeBlock != null)
                ChangeBlock(nextblock);

            await nextblock.InitializeBlockAsync(lastblock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));

            return nextblock;
        }
    }
}
