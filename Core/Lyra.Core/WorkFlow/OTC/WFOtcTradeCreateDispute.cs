using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.Identity;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.OTC
{
    [LyraWorkFlow]//v
    public class WFOtcTradeCreateDispute : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_CRDPT,
                RecvVia = BrokerRecvType.None,
                Steps = new [] { ChangeStateAsync }
            };
        }

        // user pay via off-chain ways and confirm payment in OTC trade.
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (send.Tags.Count != 1)
                return APIResultCodes.InvalidBlockTags;

            var tradeid = send.DestinationAccountId;
            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid) as IOtcTrade;
            if (tradeblk == null)
                return APIResultCodes.InvalidTrade;
            
            if (tradeblk.OwnerAccountId != send.AccountID &&
                tradeblk.Trade.orderOwnerId != send.AccountID
                )
                return APIResultCodes.NotOwnerOfTrade;

            // can't reopen closed dispute trade
            if (tradeblk.OTStatus == OTCTradeStatus.Dispute ||
                tradeblk.OTStatus == OTCTradeStatus.DisputeClosed)
                return APIResultCodes.InvalidTradeStatus;

            // and the dispute was not raised to lyra council
            if (Neo.Settings.Default.LyraNode.Lyra.NetworkId != "xtest" && !string.IsNullOrEmpty(tradeblk.Trade.dealerId))
            {
                // check if trade is cancellable
                var lsb = sys.Storage.GetLastServiceBlock();
                var wallet = sys.PosWallet;
                var sign = Signatures.GetSignature(wallet.PrivateKey, lsb.Hash, wallet.AccountId);
                var dlrblk = await sys.Storage.FindLatestBlockAsync(tradeblk.Trade.dealerId);
                var uri = new Uri(new Uri((dlrblk as IDealer).Endpoint), "/api/dealer/");
                var dealer = new DealerClient(uri);
                var ret = await dealer.GetTradeBriefAsync(tradeid, wallet.AccountId, sign);
                if (!ret.Successful())
                    return APIResultCodes.InvalidOperation;

                var brief = ret.Deserialize<TradeBrief>();
                if (brief == null)
                    return APIResultCodes.InvalidOperation;

                if (brief.DisputeLevel != DisputeLevels.Peer)
                    return APIResultCodes.InvalidOperation;
            }

            return APIResultCodes.Success;
        }

        protected async Task<TransactionBlock> ChangeStateAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

            var prevBlock = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId) as TransactionBlock;
            var votblk = await TransactionOperateAsync(sys, send.Hash, prevBlock,
                () => prevBlock.GenInc<OtcTradeRecvBlock>(),
                () => WFState.Finished,
                (b) =>
                {
                    // recv
                    (b as ReceiveTransferBlock).SourceHash = send.Hash;

                    // broker
                    (b as IBrokerAccount).RelatedTx = send.Hash;

                    // trade
                    (b as IOtcTrade).OTStatus = OTCTradeStatus.Dispute;

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
