using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.ODR;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.OTC
{
    [LyraWorkFlow]//v
    public class WFOtcTradeResolveDispute : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_RSLDPT,
                RecvVia = BrokerRecvType.None,
            };
        }

        // user pay via off-chain ways and confirm payment in OTC trade.
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (send.Tags.Count != 3 || 
                !send.Tags.ContainsKey("data") || 
                string.IsNullOrEmpty(send.Tags["data"]) ||
                !send.Tags.ContainsKey("voteid") ||
                string.IsNullOrEmpty(send.Tags["voteid"]))
                return APIResultCodes.InvalidBlockTags;

            var tradeid = send.DestinationAccountId;
            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid);
            if (tradeblk == null || tradeblk is not IOtcTrade)
                return APIResultCodes.InvalidTrade;
            
            // shoult not be the litigant
            if ((tradeblk as IBrokerAccount).OwnerAccountId == send.AccountID ||
                (tradeblk as IOtcTrade).Trade.orderOwnerId == send.AccountID
                )
                return APIResultCodes.Unauthorized;

            var voteid = send.Tags["voteid"];
            // check who execute the vote result
            if (send.AccountID != LyraGlobal.LORDACCOUNTID)
            {
                // check vote status
                var vs = await sys.Storage.GetVoteSummaryAsync(voteid);
                if (!vs.IsDecided)
                    return APIResultCodes.Unauthorized;
            }

            if ((tradeblk as IOtcTrade).OTStatus != OTCTradeStatus.Dispute)
                return APIResultCodes.InvalidTradeStatus;

            // verify resolution
            var resolution = JsonConvert.DeserializeObject<ODRResolution>(send.Tags["data"]);
            if (resolution == null || resolution.actions == null || resolution.actions.Length == 0)
                return APIResultCodes.InvalidOperation;

            foreach(var act in resolution.actions)
            {
                if (act.amount <= 0)
                    return APIResultCodes.InvalidOperation;
            }

            // should not execute more than once            
            var exec = await sys.Storage.FindExecForVoteAsync(voteid);
            if (exec != null)
                return APIResultCodes.AlreadyExecuted;

            return APIResultCodes.Success;
        }

        public override async Task<TransactionBlock> MainProcAsync(DagSystem sys, SendTransferBlock send, LyraContext context)
        {
            return await ChangeStateAsync(sys, send) ?? await GetBlocksAsync(sys, send);
        }

        private async Task<TransactionBlock> GetBlocksAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var resolv = JsonConvert.DeserializeObject<ODRResolution>(send.Tags["data"]);
            if (blocks.Count < resolv.actions.Length + 1)
            {
                // populate tos
                var tradegen = await sys.Storage.FindFirstBlockAsync(resolv.tradeid) as IOtcTrade;
                var tos = new Dictionary<Parties, string>
                {
                    { Parties.Seller, tradegen.Trade.orderOwnerId },
                    { Parties.Buyer, (tradegen as IBrokerAccount).OwnerAccountId },
                    { Parties.DAOTreasure, (tradegen as TransactionBlock).AccountID }
                };

                return await SlashCollateral(sys, send,
                    tos[resolv.actions[blocks.Count - 1].to], resolv.actions[blocks.Count - 1].amount);
            }
            else
                return null;
        }

        protected async Task<TransactionBlock> SlashCollateral(DagSystem sys, SendTransferBlock send, string to, decimal amount)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var resolv = JsonConvert.DeserializeObject<ODRResolution>(send.Tags["data"]);

            var tradelatest = await sys.Storage.FindLatestBlockAsync(resolv.tradeid) as IOtcTrade;
            var daolatest = await sys.Storage.FindLatestBlockAsync(tradelatest.Trade.daoId) as TransactionBlock;

            var daosendblk = await TransactionOperateAsync(sys, send.Hash, daolatest,
                () => daolatest.GenInc<DaoSendBlock>(),
                (b) =>
                {
                    // send
                    (b as SendTransferBlock).DestinationAccountId = to;

                    // broker
                    (b as IBrokerAccount).RelatedTx = send.Hash;

                    var oldbalance = daolatest.Balances.ToDecimalDict();
                    oldbalance["LYR"] -= amount;
                    b.Balances = oldbalance.ToLongDict();
                });
            return daosendblk;
        }

        protected async Task<TransactionBlock> ChangeStateAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            if (blocks.Count > 0)
                return null;

            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

            var prevBlock = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId) as TransactionBlock;
            var votblk = await TransactionOperateAsync(sys, send.Hash, prevBlock,
                () => prevBlock.GenInc<OtcVotedResolutionBlock>(),
                (b) =>
                {
                    // recv
                    (b as ReceiveTransferBlock).SourceHash = send.Hash;

                    // trade
                    (b as IOtcTrade).OTStatus = OTCTradeStatus.DisputeClosed;

                    (b as IVoteExec).voteid = send.Tags["voteid"];

                    var oldbalance = prevBlock.Balances.ToDecimalDict();
                    if (oldbalance.ContainsKey("LYR"))
                        oldbalance["LYR"] += txInfo.Changes["LYR"];
                    else
                        oldbalance.Add("LYR", txInfo.Changes["LYR"]);
                    b.Balances = oldbalance.ToLongDict();
                });
            return votblk;
        }
    }
}
