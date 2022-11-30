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
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace Lyra.Core.WorkFlow.Uni
{
    [LyraWorkFlow]//v
    public class WFUniTradeConfirmGotPay : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_UNI_TRDPAYGOT,
                RecvVia = BrokerRecvType.None,
                Steps = new[] { 
                    ChangeStateToBidReceivedAsync, 
                    SendCryptoProductFromTradeToBuyerAsync, 
                    SendCollateralFromDAOToTradeOwnerAsync
                }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (send.Tags == null)
                throw new ArgumentNullException();

            if (send.Tags == null || send.Tags.Count != 4 ||
                !send.Tags.ContainsKey("tradeid") ||
                string.IsNullOrWhiteSpace(send.Tags["tradeid"]) ||
                !send.Tags.ContainsKey("pod") ||
                string.IsNullOrWhiteSpace(send.Tags["pod"]) ||
                !send.Tags.ContainsKey("sign") ||
                string.IsNullOrWhiteSpace(send.Tags["sign"])
                )
                return APIResultCodes.InvalidBlockTags;

            var tradeid = send.Tags["tradeid"];
            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid);
            if (tradeblk == null)
                return APIResultCodes.InvalidTrade;

            var trade = tradeblk as IUniTrade;
            if (trade == null)
                return APIResultCodes.InvalidParameterFormat;

            // only fiat trade is OTC.
            if (trade == null || !LyraGlobal.GetOTCRequirementFromTicker(trade.Trade.biding))
                return APIResultCodes.InvalidTradeStatus;

            // check if seller is the order's owner
            var orderid = trade.Trade.orderId;
            var orderblk = await sys.Storage.FindLatestBlockAsync(orderid);
            if (orderblk == null)
                return APIResultCodes.InvalidOrder;

            var order = orderblk as IUniOrder;
            if (order == null)
                return APIResultCodes.InvalidParameterFormat;

            if (trade.UTStatus != UniTradeStatus.Processing)
                return APIResultCodes.InvalidTradeStatus;

            if(order.OwnerAccountId != send.AccountID && trade.OwnerAccountId != send.AccountID)
            {
                return APIResultCodes.NotSellerOfTrade;
            }

            PoDCatalog catalog;
            if (!Enum.TryParse<PoDCatalog>(send.Tags["pod"], out catalog))
            {
                return APIResultCodes.InvalidBlockTags;
            }

            if (trade.OwnerAccountId == send.AccountID && catalog != PoDCatalog.OfferReceived) // must be bidsent
            {
                return APIResultCodes.InvalidProofOfDelivery;
            }

            if (trade.Trade.orderOwnerId == send.AccountID && catalog != PoDCatalog.BidReceived)
            {
                return APIResultCodes.InvalidProofOfDelivery;
            }

            return APIResultCodes.Success;
        }

        public override async Task<ReceiveTransferBlock?> NormalReceiveAsync(DagSystem sys, LyraContext context)
        {
            return await ChangeStateToBidReceivedAsync(sys, context) as ReceiveTransferBlock;
        }

        public override async Task<ReceiveTransferBlock> RefundReceiveAsync(DagSystem sys, LyraContext context)
        {
            return await ChangeStateToBidReceivedAsync(sys, context) as ReceiveTransferBlock;
        }

        private async Task<bool> CheckTradeDone(DagSystem sys, LyraContext context)
        {
            var tradeid = context.Send.Tags["tradeid"];
            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid);
            var trade = tradeblk as IUniTrade;
            bool bidIsOk;
            if (LyraGlobal.GetOTCRequirementFromTicker(trade.Trade.biding))
            {
                // should has bid received
                bidIsOk = trade.Delivery.Proofs.ContainsKey(PoDCatalog.BidSent) 
                    && trade.Delivery.Proofs.ContainsKey(PoDCatalog.BidReceived);
            }
            else
            {
                bidIsOk = true;
            }


            bool offerIsOk;
            if (LyraGlobal.GetOTCRequirementFromTicker(trade.Trade.offering))
            {
                // should has bid received
                offerIsOk = trade.Delivery.Proofs.ContainsKey(PoDCatalog.OfferSent)
                    && trade.Delivery.Proofs.ContainsKey(PoDCatalog.OfferReceived);
            }
            else
            {
                offerIsOk = true;
            }

            return offerIsOk && bidIsOk;
        }

        protected async Task<TransactionBlock?> SendCryptoProductFromTradeToBuyerAsync(DagSystem sys, LyraContext context)
        {
            if (!await CheckTradeDone(sys, context))
                return null;

            var sendBlock = context.Send;
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;
            
            return await TransactionOperateAsync(sys, sendBlock.Hash, lastblock,
                () => lastblock.GenInc<UniTradeSendBlock>(),
                () => context.State,
                (b) =>
                {
                    var trade = b as IUniTrade;

                    trade.UTStatus = UniTradeStatus.Closed;

                    (b as SendTransferBlock).DestinationAccountId = trade.OwnerAccountId;

                    b.Balances[trade.Trade.offering] = 0;
                });
        }

        protected async Task<TransactionBlock?> SendCollateralFromDAOToTradeOwnerAsync(DagSystem sys, LyraContext context)
        {
            if (!await CheckTradeDone(sys, context))
                return null;

            var send = context.Send;
            var tradelatest = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId) as TransactionBlock;

            var trade = (tradelatest as IUniTrade).Trade;
            var daolastblock = await sys.Storage.FindLatestBlockAsync(trade.daoId) as TransactionBlock;

            // get dao for order genesis
            var odrgen = await sys.Storage.FindFirstBlockAsync(trade.orderId) as ReceiveTransferBlock;
            var daoforodr = await sys.Storage.FindBlockByHashAsync(odrgen.SourceHash) as IDao;

            // buyer fee calculated as LYR
            var totalAmount = trade.amount;
            decimal totalFee = 0;
            decimal networkFee = 0;
            var order = (odrgen as IUniOrder).Order;
            // transaction fee

            totalFee += Math.Round(trade.cltamt * daoforodr.BuyerFeeRatio, 8);
            networkFee = Math.Round(trade.cltamt * LyraGlobal.BidingNetworkFeeRatio, 8);

            Console.WriteLine($"buyer pay svc fee {totalFee} and net fee {networkFee}");

            var amountToSeller = trade.cltamt - totalFee;
            //Console.WriteLine($"collateral: {trade.collateral} txfee: {totalFee} netfee: {networkFee} remains: {trade.collateral - totalFee - networkFee} cost: {totalFee + networkFee }");

            return await TransactionOperateAsync(sys, send.Hash, daolastblock,
                () => daolastblock.GenInc<DaoSendBlock>(),
                () => context.State,
                (b) =>
                {
                    // block
                    b.Fee = networkFee;
                    b.FeeType = AuthorizationFeeTypes.Dynamic;

                    // recv
                    (b as SendTransferBlock).DestinationAccountId = (tradelatest as IUniTrade).OwnerAccountId;

                    // balance
                    var oldbalance = b.Balances.ToDecimalDict();
                    oldbalance[LyraGlobal.OFFICIALTICKERCODE] -= amountToSeller;
                    b.Balances = oldbalance.ToLongDict();
                });
        }

        protected async Task<TransactionBlock?> ChangeStateToBidReceivedAsync(DagSystem sys, LyraContext context)
        {
            var sendBlock = context.Send;
            Console.WriteLine("Exec: ChangeStateToBidReceivedAsync");
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            return await TransactionOperateAsync(sys, sendBlock.Hash, lastblock,
                () => lastblock.GenInc<UniTradeRecvBlock>(),
                () => context.State,
                (b) =>
                {
                    // recv
                    b.SourceHash = sendBlock.Hash;
                    var trade = b as IUniTrade;

                    var txInfo = sendBlock.GetBalanceChanges(sys.Storage.FindBlockByHash(sendBlock.PreviousHash) as TransactionBlock);

                    var recvBalances = b.Balances.ToDecimalDict();
                    foreach (var chg in txInfo.Changes)
                    {
                        if (recvBalances.ContainsKey(chg.Key))
                            recvBalances[chg.Key] += chg.Value;
                        else
                            recvBalances.Add(chg.Key, chg.Value);
                    }
                    b.Balances = recvBalances.ToLongDict();

                    // no, don't change anything.
                    //(b as IUniTrade).UTStatus = UniTradeStatus.Closed;

                    // if refund receive, attach a refund reason.
                    if (context.State == WFState.NormalReceive || context.State == WFState.RefundReceive)
                    {
                        b.AddTag("auth", context.AuthResult.Result.ToString());
                    }

                    if (context.State == WFState.NormalReceive)
                    {
                        PoDCatalog catalog = Enum.Parse<PoDCatalog>(sendBlock.Tags["pod"]);
                        trade.Delivery.Add(catalog, sendBlock.Tags["sign"]);
                    }
                });
        }
    }
}
