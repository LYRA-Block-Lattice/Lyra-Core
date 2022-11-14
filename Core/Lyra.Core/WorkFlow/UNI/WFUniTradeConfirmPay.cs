using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.Uni
{
    [LyraWorkFlow]//v
    public class WFUniTradeConfirmPay : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_UNI_TRDPAYSENT,
                RecvVia = BrokerRecvType.None,
                Steps = new [] { ChangeStateAsync }
            };
        }

        // user pay via off-chain ways and confirm payment in Uni trade.
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (send.Tags.Count != 2 ||
                !send.Tags.ContainsKey("tradeid") ||
                string.IsNullOrWhiteSpace(send.Tags["tradeid"]))
                return APIResultCodes.InvalidBlockTags;

            var tradeid = send.Tags["tradeid"];
            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid);
            if (tradeblk == null)
                return APIResultCodes.InvalidTrade;

            var trade = tradeblk as IUniTrade;
            if (trade.OwnerAccountId != send.AccountID)
                return APIResultCodes.NotOwnerOfTrade;

            if ((tradeblk as IUniTrade).UTStatus != UniTradeStatus.Open)
                return APIResultCodes.InvalidTradeStatus;

            return APIResultCodes.Success;
        }

        protected async Task<TransactionBlock> ChangeStateAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(sendBlock.Hash);
            if (recv != null)
                return null;

            var txInfo = sendBlock.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock);
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            return await TransactionOperateAsync(sys, sendBlock.Hash, lastblock,
                () => lastblock.GenInc<UniTradeRecvBlock>(),
                () => WFState.Finished,
                (b) =>
                {
                    (b as ReceiveTransferBlock).SourceHash = sendBlock.Hash;
                    (b as IUniTrade).UTStatus = UniTradeStatus.BidSent;

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
