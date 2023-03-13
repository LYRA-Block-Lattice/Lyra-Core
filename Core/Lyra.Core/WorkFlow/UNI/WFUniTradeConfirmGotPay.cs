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
                    SendPropsFromTradeToBuyerAsync, 
                    SendPropsFromTradeToSellerAsync,
                    SendFeesToDaoAsync
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

            //// only fiat trade is OTC.
            //if (trade == null || !LyraGlobal.GetOTCRequirementFromTicker(trade.Trade.biding))
            //    return APIResultCodes.InvalidTradeStatus;

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

        protected async Task<TransactionBlock?> SendPropsFromTradeToBuyerAsync(DagSystem sys, LyraContext context)
        {
            if (!await CheckTradeDone(sys, context))
                return null;

            var sendBlock = context.Send;
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            // calculate fees
            var (sf, bf, snf, bnf) = await CalculateFeesAsync(sys, context);

            return await TransactionOperateAsync(sys, sendBlock.Hash, lastblock,
                () => lastblock.GenInc<UniTradeSendBlock>(),
                () => context.State,
                (b) =>
                {
                    var trade = b as IUniTrade;

                    (b as SendTransferBlock).DestinationAccountId = trade.OwnerAccountId;

                    b.Balances[trade.Trade.offering] -= trade.Trade.amount.ToBalanceLong();
                    b.Balances["LYR"] -= (trade.Trade.cltamt - bf - bnf).ToBalanceLong();
                });
        }

        protected async Task<TransactionBlock?> SendPropsFromTradeToSellerAsync(DagSystem sys, LyraContext context)
        {
            if (!await CheckTradeDone(sys, context))
                return null;

            var sendBlock = context.Send;
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            // calculate fees
            var (sf, bf, snf, bnf) = await CalculateFeesAsync(sys, context);
            
            return await TransactionOperateAsync(sys, sendBlock.Hash, lastblock,
                () => lastblock.GenInc<UniTradeSendBlock>(),
                () => context.State,
                (b) =>
                {
                    var trade = b as IUniTrade;

                    (b as SendTransferBlock).DestinationAccountId = trade.Trade.orderOwnerId;

                    b.Balances[trade.Trade.biding] -= trade.Trade.pay.ToBalanceLong();
                    b.Balances["LYR"] -= (trade.OdrCltMmt.ToBalanceDecimal() - sf - snf).ToBalanceLong();
                });
        }

        protected async Task<(decimal sellerFee, decimal buyerFee, decimal sellerNetworkFee, decimal buyerNetworkFee)> CalculateFeesAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var tradelatest = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId) as TransactionBlock;

            var trade = (tradelatest as IUniTrade).Trade;

            // find dao by the time when order created.
            // first find order genesis block
            var odrgen = await sys.Storage.FindFirstBlockAsync(trade.orderId) as UniOrderGenesisBlock;
            var sndHash = odrgen.RelatedTx;
            var odrCreateSend = await sys.Storage.FindBlockByHashAsync(sndHash) as SendTransferBlock;

            var daoforodr = await sys.Storage.FindLatestBlockByTimeAsync(trade.daoId, odrCreateSend.TimeStamp) as IDao;

            // buyer fee calculated as LYR
            var buyerFee = Math.Round(daoforodr.BuyerFeeRatio * trade.cltamt / (daoforodr.BuyerPar / 100), 8);
            var sellerFee = Math.Round(daoforodr.SellerFeeRatio * trade.cltamt / (daoforodr.SellerPar / 100), 8);
            var buyerNetworkFee = Math.Round(LyraGlobal.BidingNetworkFeeRatio * trade.cltamt / (daoforodr.BuyerPar / 100), 8);
            var sellerNetworkFee = Math.Round(LyraGlobal.OfferingNetworkFeeRatio * trade.cltamt / (daoforodr.SellerPar / 100), 8);

            return (sellerFee, buyerFee, sellerNetworkFee, buyerNetworkFee);
        }

        protected async Task<TransactionBlock?> SendFeesToDaoAsync(DagSystem sys, LyraContext context)
        {
            if (!await CheckTradeDone(sys, context))
                return null;

            var sendBlock = context.Send;
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            // calculate fees
            var (sf, bf, snf, bnf) = await CalculateFeesAsync(sys, context);
            
            return await TransactionOperateAsync(sys, sendBlock.Hash, lastblock,
                () => lastblock.GenInc<UniTradeSendBlock>(),
                () => context.State,
                (b) =>
                {
                    // pay network fees
                    b.FeeType = AuthorizationFeeTypes.Dynamic;
                    b.Fee = snf + bnf;
                    
                    var trade = b as IUniTrade;

                    trade.UTStatus = UniTradeStatus.Closed;
                    (b as SendTransferBlock).DestinationAccountId = trade.Trade.daoId;

                    b.Balances["LYR"] = 0;
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
