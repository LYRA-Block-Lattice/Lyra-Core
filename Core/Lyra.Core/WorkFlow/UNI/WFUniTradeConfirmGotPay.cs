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
                    SendCryptoProductFromTradeToBuyerAsync, 
                    SendCollateralFromDAOToTradeOwnerAsync
                }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send)
        {
            if (send.Tags == null)
                throw new ArgumentNullException();

            if (send.Tags.Count != 2 ||
                !send.Tags.ContainsKey("tradeid") ||
                string.IsNullOrWhiteSpace(send.Tags["tradeid"]))
                return APIResultCodes.InvalidBlockTags;

            var tradeid = send.Tags["tradeid"];
            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid);
            if (tradeblk == null)
                return APIResultCodes.InvalidTrade;

            var trade = tradeblk as IUniTrade;
            if (trade == null)
                return APIResultCodes.InvalidParameterFormat;

            // only fiat trade is OTC.
            if (trade == null || !trade.Trade.biding.ToLower().StartsWith("fiat/"))
                return APIResultCodes.InvalidTradeStatus;

            // check if seller is the order's owner
            var orderid = trade.Trade.orderId;
            var orderblk = await sys.Storage.FindLatestBlockAsync(orderid);
            if (orderblk == null)
                return APIResultCodes.InvalidOrder;

            var order = orderblk as IUniOrder;
            if (order == null)
                return APIResultCodes.InvalidParameterFormat;

            if (trade.UTStatus != UniTradeStatus.BidSent)
                return APIResultCodes.InvalidTradeStatus;

            if(order.OwnerAccountId != send.AccountID)
            {
                return APIResultCodes.NotSellerOfTrade;
            }

            return APIResultCodes.Success;
        }

        protected async Task<TransactionBlock> SendCryptoProductFromTradeToBuyerAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;
            
            return await TransactionOperateAsync(sys, sendBlock.Hash, lastblock,
                () => lastblock.GenInc<UniTradeSendBlock>(),
                () => WFState.Running,
                (b) =>
                {
                    var trade = b as IUniTrade;

                    trade.UTStatus = UniTradeStatus.OfferReceived;

                    (b as SendTransferBlock).DestinationAccountId = trade.OwnerAccountId;

                    b.Balances[trade.Trade.offering] = 0;
                });
        }

        protected async Task<TransactionBlock> SendCollateralFromDAOToTradeOwnerAsync(DagSystem sys, SendTransferBlock send)
        {
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
                () => WFState.Finished,
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

        protected async Task<TransactionBlock> ChangeStateToBidReceivedAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            Console.WriteLine("Exec: ChangeStateToBidReceivedAsync");
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            return await TransactionOperateAsync(sys, sendBlock.Hash, lastblock,
                () => lastblock.GenInc<UniTradeRecvBlock>(),
                () => WFState.Running,
                (b) =>
                {
                    // recv
                    (b as ReceiveTransferBlock).SourceHash = sendBlock.Hash;

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

                    (b as IUniTrade).UTStatus = UniTradeStatus.BidReceived;
                });
        }
    }
}
