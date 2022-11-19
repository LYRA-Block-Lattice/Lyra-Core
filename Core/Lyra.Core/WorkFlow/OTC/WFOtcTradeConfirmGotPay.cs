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
    public class WFOtcTradeConfirmGotPay : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_TRDPAYGOT,
                RecvVia = BrokerRecvType.None,
                Steps = new[] { 
                    ChangeStateAsync, 
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

            if (send.Tags.Count != 2 ||
                !send.Tags.ContainsKey("tradeid") ||
                string.IsNullOrWhiteSpace(send.Tags["tradeid"]))
                return APIResultCodes.InvalidBlockTags;

            var tradeid = send.Tags["tradeid"];
            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid);
            if (tradeblk == null)
                return APIResultCodes.InvalidTrade;

            var trade = tradeblk as IOtcTrade;
            if (trade == null)
                return APIResultCodes.InvalidParameterFormat;

            // check if seller is the order's owner
            var orderid = trade.Trade.orderId;
            var orderblk = await sys.Storage.FindLatestBlockAsync(orderid);
            if (orderblk == null)
                return APIResultCodes.InvalidOrder;

            var order = orderblk as IOtcOrder;
            if (order == null)
                return APIResultCodes.InvalidParameterFormat;

            if (trade.OTStatus != OTCTradeStatus.FiatSent)
                return APIResultCodes.InvalidTradeStatus;

            if(trade.Trade.dir == TradeDirection.Buy && order.OwnerAccountId != send.AccountID)
            {
                return APIResultCodes.NotSellerOfTrade;
            }
            if(trade.Trade.dir == TradeDirection.Sell && trade.OwnerAccountId != send.AccountID)
            {
                return APIResultCodes.NotOwnerOfTrade;
            }

            return APIResultCodes.Success;
        }

        protected async Task<TransactionBlock?> SendCryptoProductFromTradeToBuyerAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var lastblock = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId) as TransactionBlock;

            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<OtcTradeSendBlock>(),
                () => WFState.Running,
                (b) =>
                {
                    var trade = b as IOtcTrade;

                    trade.OTStatus = OTCTradeStatus.CryptoReleased;

                    if(trade.Trade.dir == TradeDirection.Buy)
                        (b as SendTransferBlock).DestinationAccountId = trade.OwnerAccountId;
                    else
                        (b as SendTransferBlock).DestinationAccountId = trade.Trade.orderOwnerId;

                    b.Balances[trade.Trade.crypto] = 0;
                });
        }

        protected async Task<TransactionBlock?> SendCollateralFromDAOToTradeOwnerAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var tradelatest = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId) as TransactionBlock;

            var trade = (tradelatest as IOtcTrade).Trade;
            var daolastblock = await sys.Storage.FindLatestBlockAsync(trade.daoId) as TransactionBlock;

            // get dao for order genesis
            var odrgen = await sys.Storage.FindFirstBlockAsync(trade.orderId) as ReceiveTransferBlock;
            var daoforodr = await sys.Storage.FindBlockByHashAsync(odrgen.SourceHash) as IDao;

            // buyer fee calculated as LYR
            var totalAmount = trade.amount;
            decimal totalFee = 0;
            var order = (odrgen as IOtcOrder).Order;
            // transaction fee
            if (trade.dir == TradeDirection.Sell)
            {
                totalFee += Math.Round((((totalAmount * trade.price) * order.fiatPrice) * daoforodr.SellerFeeRatio) / order.collateralPrice, 8);
            }
            else
            {
                totalFee += Math.Round((((totalAmount * trade.price) * order.fiatPrice) * daoforodr.BuyerFeeRatio) / order.collateralPrice, 8);
            }

            // network fee
            var networkFee = Math.Round((((totalAmount * order.price) * order.fiatPrice) * 0.002m) / order.collateralPrice, 8);

            var amountToSeller = trade.collateral - totalFee;
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
                    (b as SendTransferBlock).DestinationAccountId = (tradelatest as IOtcTrade).OwnerAccountId;

                    // balance
                    var oldbalance = b.Balances.ToDecimalDict();
                    oldbalance[LyraGlobal.OFFICIALTICKERCODE] -= amountToSeller;
                    b.Balances = oldbalance.ToLongDict();
                });
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
            var send = context.Send;
            var lastblock = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId) as TransactionBlock;

            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<OtcTradeRecvBlock>(),
                () => WFState.Running,
                (b) =>
                {
                    // recv
                    (b as ReceiveTransferBlock).SourceHash = send.Hash;

                    var txInfo = send.GetBalanceChanges(sys.Storage.FindBlockByHash(send.PreviousHash) as TransactionBlock);

                    var recvBalances = b.Balances.ToDecimalDict();
                    foreach (var chg in txInfo.Changes)
                    {
                        if (recvBalances.ContainsKey(chg.Key))
                            recvBalances[chg.Key] += chg.Value;
                        else
                            recvBalances.Add(chg.Key, chg.Value);
                    }
                    b.Balances = recvBalances.ToLongDict();

                    (b as IOtcTrade).OTStatus = OTCTradeStatus.FiatReceived;
                });
        }

        //protected async Task<TransactionBlock> TradeBlockOperateAsyncx(
        //    DagSystem sys, 
        //    SendTransferBlock sendBlock,
        //    Func<TransactionBlock> GenBlock,
        //    Action<TransactionBlock> ChangeBlock
        //    )
        //{
        //    var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

        //    var lsb = await sys.Storage.GetLastServiceBlockAsync();

        //    var nextblock = GenBlock();

        //    // block
        //    nextblock.ServiceHash = lsb.Hash;

        //    // transaction
        //    nextblock.AccountID = sendBlock.DestinationAccountId;
        //    nextblock.Balances = new Dictionary<string, long>();
        //    nextblock.Fee = 0;
        //    nextblock.FeeCode = LyraGlobal.OFFICIALTICKERCODE;
        //    nextblock.FeeType = AuthorizationFeeTypes.NoFee;

        //    // broker
        //    (nextblock as IBrokerAccount).Name = ((IBrokerAccount)lastblock).Name;
        //    (nextblock as IBrokerAccount).OwnerAccountId = ((IBrokerAccount)lastblock).OwnerAccountId;
        //    (nextblock as IBrokerAccount).RelatedTx = sendBlock.Hash;

        //    // trade     
        //    (nextblock as IOtcTrade).Trade = ((IOtcTrade)lastblock).Trade;
        //    (nextblock as IOtcTrade).OTStatus = ((IOtcTrade)lastblock).OTStatus;

        //    nextblock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

        //    var latestBalances = lastblock.Balances.ToDecimalDict();
        //    var recvBalances = lastblock.Balances.ToDecimalDict();
        //    nextblock.Balances = recvBalances.ToLongDict();

        //    ChangeBlock(nextblock);

        //    await nextblock.InitializeBlockAsync(lastblock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));

        //    return nextblock;
        //}
    }
}
