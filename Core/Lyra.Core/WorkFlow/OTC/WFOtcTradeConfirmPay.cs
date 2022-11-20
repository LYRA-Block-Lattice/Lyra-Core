using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace Lyra.Core.WorkFlow.OTC
{
    [LyraWorkFlow]//v
    public class WFOtcTradeConfirmPay : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_TRDPAYSENT,
                RecvVia = BrokerRecvType.None,
                Steps = new [] { ChangeStateAsync }
            };
        }

        // user pay via off-chain ways and confirm payment in OTC trade.
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (send.Tags.Count != 2 ||
                !send.Tags.ContainsKey("tradeid") ||
                string.IsNullOrWhiteSpace(send.Tags["tradeid"]))
                return APIResultCodes.InvalidBlockTags;

            var tradeid = send.Tags["tradeid"];
            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid);
            if (tradeblk == null)
                return APIResultCodes.InvalidTrade;

            var trade = tradeblk as IOtcTrade;
            if (trade.Trade.dir == TradeDirection.Buy && trade.OwnerAccountId != send.AccountID)
                return APIResultCodes.NotOwnerOfTrade;

            if (trade.Trade.dir == TradeDirection.Sell && trade.Trade.orderOwnerId != send.AccountID)
                return APIResultCodes.NotOwnerOfOrder;

            if ((tradeblk as IOtcTrade).OTStatus != OTCTradeStatus.Open)
                return APIResultCodes.InvalidTradeStatus;

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

        protected async Task<TransactionBlock?> ChangeStateAsync(DagSystem sys, LyraContext context)
        {
            var sendBlock = context.Send;
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(sendBlock.Hash);
            if (recv != null)
                return null;

            var txInfo = sendBlock.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock);
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            return await TransactionOperateAsync(sys, sendBlock.Hash, lastblock,
                () => lastblock.GenInc<OtcTradeRecvBlock>(),
                () => context.State,
                (b) =>
                {
                    (b as ReceiveTransferBlock).SourceHash = sendBlock.Hash;
                    (b as IOtcTrade).OTStatus = OTCTradeStatus.FiatSent;

                    // calculate balance
                    var latestBalances = lastblock.Balances.ToDecimalDict();
                    var recvBalances = lastblock.Balances.ToDecimalDict();
                    foreach (var chg in txInfo.Changes)
                    {
                        if (recvBalances.ContainsKey(chg.Key))
                            recvBalances[chg.Key] += chg.Value;
                        else
                            recvBalances.Add(chg.Key, chg.Value);
                    }

                    b.Balances = recvBalances.ToLongDict();
                });
        }
    }
}
