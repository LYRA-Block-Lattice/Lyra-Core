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
    [LyraWorkFlow]//v
    public class WFStakingUnStaking : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_STK_UNSTK,
                RecvVia = BrokerRecvType.GuildRecv,
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var block = context.Send;
            TransactionBlock last = await DagSystem.Singleton.Storage.FindBlockByHashAsync(block.PreviousHash) as TransactionBlock;

            var chgs = block.GetBalanceChanges(last);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return APIResultCodes.InvalidFeeAmount;

            if (
                block.Tags.ContainsKey("stkid") && !string.IsNullOrWhiteSpace(block.Tags["stkid"])
                && block.Tags.Count == 2
                )
            {
                if (block.DestinationAccountId != LyraGlobal.GUILDACCOUNTID)
                    return APIResultCodes.InvalidServiceRequest;

                // verify sender is the owner of stkingblock
                var stks = await sys.Storage.FindAllStakingAccountForOwnerAsync(block.AccountID);
                if (!stks.Any(a => a.AccountID == block.Tags["stkid"]))
                    return APIResultCodes.InvalidStakingAccount;

                var lastStk = await sys.Storage.FindLatestBlockAsync(block.Tags["stkid"]) as TransactionBlock;
                if (lastStk == null)
                    return APIResultCodes.InvalidUnstaking;

                if(lastStk.BlockType == BlockTypes.UnStaking || ((IStaking)lastStk).Start == DateTime.MaxValue)
                    return APIResultCodes.InvalidUnstaking;

                if (!lastStk.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) || lastStk.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() == 0)
                    return APIResultCodes.InvalidUnstaking;   
            }
            else
                return APIResultCodes.InvalidBlockTags;

            return APIResultCodes.Success;
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;

            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            if (blocks.Any(a => a is UnStakingBlock))
                return null;

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendPrev = await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;
            var stkId = send.Tags["stkid"];
            var lastStk = await sys.Storage.FindLatestBlockAsync(stkId) as TransactionBlock;

            var stkNext = new UnStakingBlock
            {
                Height = lastStk.Height + 1,
                Name = (lastStk as IBrokerAccount).Name,
                OwnerAccountId = (lastStk as IBrokerAccount).OwnerAccountId,
                AccountID = lastStk.AccountID,
                Balances = new Dictionary<string, long>(),
                PreviousHash = lastStk.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.Dynamic,
                DestinationAccountId = send.AccountID,

                // pool specified config
                Days = (lastStk as IStaking).Days,
                Voting = (lastStk as IStaking).Voting,
                RelatedTx = send.Hash,
                Start = DateTime.MaxValue,
                CompoundMode = ((IStaking)lastStk).CompoundMode
            };

            if (((IStaking)lastStk).Start.AddDays(((IStaking)lastStk).Days) > DateTime.UtcNow)
            {
                stkNext.Fee = Math.Round(0.008m * lastStk.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal(), 8, MidpointRounding.ToZero);
            }

            stkNext.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, 0);

            stkNext.AddTag(Block.MANAGEDTAG, context.State.ToString()); 

            // pool blocks are service block so all service block signed by leader node
            stkNext.InitializeBlock(lastStk, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return stkNext;
        }
    }
}
