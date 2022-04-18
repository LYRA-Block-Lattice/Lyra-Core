using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;

namespace Lyra.Core.WorkFlow.STK
{
    [LyraWorkFlow]
    public class WFStakingAddStaking : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_STK_ADDSTK,
                RecvVia = BrokerRecvType.None,
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock block, TransactionBlock lastBlock)
        {
            var chgs = block.GetBalanceChanges(lastBlock);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return APIResultCodes.InvalidFeeAmount;

            if (block.Tags.Count == 1)
            {
                // verify sender is the owner of stkingblock
                var stks = await sys.Storage.FindAllStakingAccountForOwnerAsync(block.AccountID);
                if (!stks.Any(a => a.AccountID == block.DestinationAccountId))
                    return APIResultCodes.InvalidStakingAccount;
            }
            else
                return APIResultCodes.InvalidBlockTags;

            return APIResultCodes.Success;
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
        {
            return await CNOAddStakingImplAsync(sys, send, send.Hash);
        }
        public static async Task<TransactionBlock> CNOAddStakingImplAsync(DagSystem sys, SendTransferBlock send, string relatedTx)
        {
            var block = await sys.Storage.FindBlockBySourceHashAsync(send.Hash);
            if (block != null)
                return null;

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendPrev = await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;
            var lastBlock = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId);
            var lastStk = lastBlock as TransactionBlock;

            DateTime start;
            if (send is BenefitingBlock bnb)
            {
                start = (lastStk as IStaking).Start;
            }
            else
            {
                start = send.TimeStamp.AddDays(1);         // manual add staking, start after 1 day.

                if (LyraNodeConfig.GetNetworkId() == "devnet")
                    start = send.TimeStamp;        // for debug
            }

            var stkNext = new StakingBlock
            {
                Height = lastStk.Height + 1,
                Name = ((IBrokerAccount)lastStk).Name,
                OwnerAccountId = ((IBrokerAccount)lastStk).OwnerAccountId,
                //AccountType = ((IOpeningBlock)lastStk).AccountType,
                AccountID = lastStk.AccountID,
                Balances = new Dictionary<string, long>(),
                PreviousHash = lastStk.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                SourceHash = send.Hash,

                // pool specified config
                Days = (lastBlock as IStaking).Days,
                Voting = ((IStaking)lastStk).Voting,
                RelatedTx = relatedTx,
                Start = start,
                CompoundMode = ((IStaking)lastStk).CompoundMode
            };

            var chgs = send.GetBalanceChanges(sendPrev);
            stkNext.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, lastStk.Balances[LyraGlobal.OFFICIALTICKERCODE] + chgs.Changes[LyraGlobal.OFFICIALTICKERCODE].ToBalanceLong());

            stkNext.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            stkNext.InitializeBlock(lastStk, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return stkNext;
        }
    }
}
