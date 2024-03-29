﻿using Lyra.Core.API;
using Lyra.Core.Authorizers;
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
                RecvVia = BrokerRecvType.None,      // none rece should implement normal receive refund receive, and refund
                Steps = new [] { ChangeStateAsync }
            };
        }

        // user pay via off-chain ways and confirm payment in Uni trade.
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (send.Tags == null || send.Tags.Count != 4 ||
                !send.Tags.ContainsKey("tradeid") ||
                !send.Tags.ContainsKey("pod") ||
                !send.Tags.ContainsKey("sign") ||
                string.IsNullOrWhiteSpace(send.Tags["tradeid"]) ||
                string.IsNullOrWhiteSpace(send.Tags["pod"]) ||
                string.IsNullOrWhiteSpace(send.Tags["sign"])
                )
                return APIResultCodes.InvalidBlockTags;

            var tradeid = send.Tags["tradeid"];
            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid);
            if (tradeblk == null)
                return APIResultCodes.InvalidTrade;

            var trade = tradeblk as IUniTrade;
            
            //if (trade == null || !LyraGlobal.GetOTCRequirementFromTicker(trade.Trade.biding))
            //    return APIResultCodes.InvalidTradeStatus;

            PoDCatalog catalog;
            if(!Enum.TryParse<PoDCatalog>(send.Tags["pod"], out catalog))
            {
                return APIResultCodes.InvalidBlockTags;
            }

            if(trade.OwnerAccountId == send.AccountID && catalog != PoDCatalog.BidSent) // must be bidsent
            {
                return APIResultCodes.InvalidProofOfDelivery;
            }
            
            if(trade.Trade.orderOwnerId == send.AccountID && catalog != PoDCatalog.OfferSent)
            {
                return APIResultCodes.InvalidProofOfDelivery;
            }

            if (trade.OwnerAccountId != send.AccountID && trade.Trade.orderOwnerId != send.AccountID)
                return APIResultCodes.NotOwnerOfTrade;

            if (trade.UTStatus != UniTradeStatus.Open && trade.UTStatus != UniTradeStatus.Processing)
                return APIResultCodes.InvalidTradeStatus;

            // verify pod?

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
                () => lastblock.GenInc<UniTradeRecvBlock>(),
                () => context.State,
                (b) =>
                {
                    b.SourceHash = sendBlock.Hash;

                    var trade = b as IUniTrade;
                    trade.UTStatus = UniTradeStatus.Processing;

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

                    // if refund receive, attach a refund reason.
                    if (context.State == WFState.NormalReceive || context.State == WFState.RefundReceive)
                    {
                        b.AddTag("auth", context.AuthResult.Result.ToString());
                    }
                    
                    if(context.State == WFState.NormalReceive)
                    {
                        PoDCatalog catalog = Enum.Parse<PoDCatalog>(sendBlock.Tags["pod"]);
                        trade.Delivery.Add(catalog, sendBlock.Tags["sign"]);
                    }
                });
        }
    }
}
