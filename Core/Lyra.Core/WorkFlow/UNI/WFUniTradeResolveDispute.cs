using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.Identity;
using Lyra.Data.API.ODR;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Neo;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.OTC
{
    [LyraWorkFlow]//v
    public class WFUniTradeResolveDispute : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_UNI_RSLDPT,
                RecvVia = BrokerRecvType.None,
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (send.Tags.Count != 3 ||
                !send.Tags.ContainsKey("data") || 
                string.IsNullOrEmpty(send.Tags["data"]) ||
                !send.Tags.ContainsKey("voteid")) // || string.IsNullOrEmpty(send.Tags["voteid"])
                return APIResultCodes.InvalidBlockTags;

            var tradeid = send.DestinationAccountId;
            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid) as IOtcTrade;
            if (tradeblk == null || tradeblk is not IOtcTrade)
                return APIResultCodes.InvalidTrade;

            var dlr = sys.Storage.FindFirstBlock(tradeblk.Trade.dealerId) as IDealer;
            if (dlr == null)
                return APIResultCodes.InvalidDealerServer;

            //if (tradeblk.OwnerAccountId != send.AccountID &&
            //    tradeblk.Trade.orderOwnerId != send.AccountID &&
            // dealer to change the state
            if (dlr.OwnerAccountId != send.AccountID && send.AccountID != LyraGlobal.GetLordAccountId(Settings.Default.LyraNode.Lyra.NetworkId))
                return APIResultCodes.PermissionDenied;

            //// shoult not be the litigant
            //if (tradeblk.OwnerAccountId == send.AccountID ||
            //    tradeblk.Trade.orderOwnerId == send.AccountID
            //    )
            //    return APIResultCodes.Unauthorized;

            var voteid = send.Tags["voteid"];
            if (voteid == null)
                return APIResultCodes.Unauthorized;

            // check vote status
            var vs = await sys.Storage.GetVoteSummaryAsync(voteid);
            if (!vs.IsDecided)
                return APIResultCodes.Unauthorized;

            // check who execute the vote result
            if (send.AccountID != LyraGlobal.GetLordAccountId(Settings.Default.LyraNode.Lyra.NetworkId))
            {
                // and the dispute was not raised to lyra council
                if (Settings.Default.LyraNode.Lyra.NetworkId != "xtest" && !string.IsNullOrEmpty(tradeblk.Trade.dealerId))
                {
                    // check if trade is cancellable
                    var lsb = sys.Storage.GetLastServiceBlock();
                    var wallet = sys.PosWallet;
                    var sign = Signatures.GetSignature(wallet.PrivateKey, lsb.Hash, wallet.AccountId);
                    var uri = new Uri(new Uri(dlr.Endpoint), "/api/dealer/");
                    var dealer = new DealerClient(uri);
                    var ret = await dealer.GetTradeBriefAsync(tradeid, wallet.AccountId, sign);
                    if (!ret.Successful())
                        return APIResultCodes.InvalidOperation;

                    var brief = ret.Deserialize<TradeBrief>();
                    if (brief == null)
                        return APIResultCodes.InvalidOperation;

                    if (brief.DisputeLevel == DisputeLevels.LyraCouncil)
                        return APIResultCodes.DisputeLevelWasRaised;
                }
            }

            if ((tradeblk as IOtcTrade).OTStatus != OTCTradeStatus.Dispute)
                return APIResultCodes.InvalidTradeStatus;

            // verify resolution
            var resolution = JsonConvert.DeserializeObject<ODRResolution>(send.Tags["data"]);
            if (resolution == null || resolution.Actions == null || resolution.Actions.Length == 0)
                return APIResultCodes.InvalidOperation;

            foreach(var act in resolution.Actions)
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

        public override async Task<ReceiveTransferBlock?> NormalReceiveAsync(DagSystem sys, LyraContext context)
        {
            return await ChangeStateAsync(sys, context) as ReceiveTransferBlock;
        }

        public override async Task<ReceiveTransferBlock?> RefundReceiveAsync(DagSystem sys, LyraContext context)
        {
            return await ChangeStateAsync(sys, context) as ReceiveTransferBlock;
        }

        public override async Task<TransactionBlock> MainProcAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            return await ChangeStateAsync(sys, context) ?? await GetBlocksAsync(sys, context);
        }

        private async Task<TransactionBlock> GetBlocksAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var resolv = JsonConvert.DeserializeObject<ODRResolution>(send.Tags["data"]);
            if (blocks.Count < resolv.Actions.Length + 1)
            {
                // populate tos
                var tradegen = await sys.Storage.FindFirstBlockAsync(resolv.TradeId) as IOtcTrade;
                var tos = new Dictionary<Parties, string>
                {
                    { Parties.Seller, tradegen.Trade.orderOwnerId },
                    { Parties.Buyer, (tradegen as IBrokerAccount).OwnerAccountId },
                    { Parties.DAOTreasure, (tradegen as TransactionBlock).AccountID }
                };

                return await SlashCollateral(sys, context,
                    tos[resolv.Actions[blocks.Count - 1].to], resolv.Actions[blocks.Count - 1].amount);
            }
            else
                return null;
        }

        protected async Task<TransactionBlock> SlashCollateral(DagSystem sys, LyraContext context, string to, decimal amount)
        {
            var send = context.Send;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var resolv = JsonConvert.DeserializeObject<ODRResolution>(send.Tags["data"]);

            var tradelatest = await sys.Storage.FindLatestBlockAsync(resolv.TradeId) as IOtcTrade;
            var daolatest = await sys.Storage.FindLatestBlockAsync(tradelatest.Trade.daoId) as TransactionBlock;

            var daosendblk = await TransactionOperateAsync(sys, send.Hash, daolatest,
                () => daolatest.GenInc<DaoSendBlock>(),
                () => context.State,
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

        protected async Task<TransactionBlock?> ChangeStateAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            if (blocks.Count > 0)
                return null;

            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

            var prevBlock = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId) as TransactionBlock;
            var votblk = await TransactionOperateAsync(sys, send.Hash, prevBlock,
                () => prevBlock.GenInc<OtcVotedResolutionBlock>(),
                () => context.State,
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

                    // if refund receive, attach a refund reason.
                    if (context.State == WFState.NormalReceive || context.State == WFState.RefundReceive)
                    {
                        b.AddTag("auth", context.AuthResult.Result.ToString());
                    }
                });
            return votblk;
        }
    }
}
