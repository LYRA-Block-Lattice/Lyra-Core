using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.Uni
{
    [LyraWorkFlow]//v
    public class WFUniOrderClose : UniSharedWFCode
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_UNI_ORDCLOSE,
                RecvVia = BrokerRecvType.DaoRecv,
                Steps = new [] { SendFeeToDao, CloseOrderAsync }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (send.Tags.Count != 3 ||
                !send.Tags.ContainsKey("daoid") ||                                            
                !send.Tags.ContainsKey("orderid") ||
                string.IsNullOrWhiteSpace(send.Tags["orderid"])
                )
                return APIResultCodes.InvalidBlockTags;

            var daoid = send.Tags["daoid"];
            var orderid = send.Tags["orderid"];
            var daoblk = await sys.Storage.FindLatestBlockAsync(daoid);
            var orderblk = await sys.Storage.FindLatestBlockAsync(orderid);

            var ordertx = orderblk as TransactionBlock;

            // need some balance to close. old bug
            if (!ordertx.Balances.Any(a => a.Value > 0))
                return APIResultCodes.InsufficientFunds;

            if (daoblk == null || orderblk == null || 
                (orderblk as IUniOrder).Order.daoId != (daoblk as TransactionBlock).AccountID)
                return APIResultCodes.InvalidTrade;

            if ((orderblk as IBrokerAccount).OwnerAccountId != send.AccountID)
                return APIResultCodes.NotSellerOfTrade;

            // the order may has already closed because no tradable assert avaliable.
            //if ((orderblk as IUniOrder).UOStatus != UniOrderStatus.Open &&
            //    (orderblk as IUniOrder).UOStatus != UniOrderStatus.Partial &&
            //    (orderblk as IUniOrder).UOStatus != UniOrderStatus.Delist)
            //    return APIResultCodes.InvalidOrderStatus;

            var trades = await sys.Storage.FindUniTradeForOrderAsync(orderid);
            if(trades.Any())
            {
                var opened = trades
                    .Where(a => a.BlockType != BlockTypes.UniTradeGenesis)
                    .Cast<IUniTrade>()
                    .Where(a => a.UTStatus != UniTradeStatus.Canceled
                        && a.UTStatus != UniTradeStatus.Closed
                        && a.UTStatus != UniTradeStatus.DisputeClosed
                    );
                if(opened.Any())
                {
                    return APIResultCodes.TradesPending;
                }
            }
            return APIResultCodes.Success;
        }
         
        protected async Task<TransactionBlock?> SendFeeToDao(DagSystem sys, LyraContext context)
        {
            var orderid = context.Send.Tags["orderid"];
            var orderlatest = await sys.Storage.FindLatestBlockAsync(orderid) as TransactionBlock;
            var daoid = (orderlatest as IUniOrder).Order.daoId;
            var daolastblock = await sys.Storage.FindLatestBlockAsync(daoid) as TransactionBlock;

            // get dao for order genesis
            var odrgen = await sys.Storage.FindFirstBlockAsync(orderid) as ReceiveTransferBlock;
            var daoforodr = await sys.Storage.FindBlockByHashAsync(odrgen.SourceHash) as IDao;
            var order = (odrgen as IUniOrder).Order;

            // about the calculation of fee.
            // first we need fee paid by LYR. so we need the value of the token.
            // but we don't know the value of token.
            // but we know that the buyer/sell know the value.
            // so we have the fee based on the recognize of buyer/seller.
            // collateral by the seller/buyer rito, and calculate it by 100%.
            // the 100% of value is maintained by both the seller and buyer. no more, no less.

            // order owner's fee is calculated on order close.
            // trade owner's fee is calculated on trade close.
            // calculate fees
            // dao fee + network fee
            decimal totalFee = 0;
            decimal networkFee = 0;

            var allTrades = await sys.Storage.FindUniTradeForOrderAsync(orderid);
            var totalAmount = allTrades.Cast<IUniTrade>()
                .Where(a => a.UTStatus == UniTradeStatus.Closed)
                .Sum(a => a.Trade.amount);

            // transaction fee
            totalFee += Math.Round(order.cltamt * daoforodr.SellerFeeRatio, 8);
            networkFee = Math.Round(order.cltamt * LyraGlobal.OfferingNetworkFeeRatio, 8);

            return await TransSendAsync<UniOrderSendBlock>(sys, context.Send.Hash, orderid,
                daoid,
                new Dictionary<string, decimal> { { LyraGlobal.OFFICIALTICKERCODE, totalFee } },
                context.State,
                (b) =>
                {
                    // after fees, order empty. so close it.
                    if (b.Balances[LyraGlobal.OFFICIALTICKERCODE] == totalFee)
                    {
                        (b as IUniOrder).UOStatus = UniOrderStatus.Closed;
                    }                    
                }
                );
        }

        protected async Task<TransactionBlock?> CloseOrderAsync(DagSystem sys, LyraContext context)
        {
            var orderid = context.Send.Tags["orderid"];
            var lastblock = await sys.Storage.FindLatestBlockAsync(orderid) as TransactionBlock;

            // check if order is already empty
            if (lastblock.Balances[LyraGlobal.OFFICIALTICKERCODE] == 0)
                return null;

            var blockNext = await TransactionOperateAsync(sys, context.Send.Hash, lastblock,
                () => lastblock.GenInc<UniOrderSendBlock>(),
                () => context.State,
                (b) =>
                {
                    var orderb = b as IUniOrder;

                    orderb.UOStatus = UniOrderStatus.Closed;

                    orderb.Order.amount = 0;
                    orderb.Order.cltamt = 0;

                    (b as SendTransferBlock).DestinationAccountId = orderb.OwnerAccountId;

                    // when delist, the crypto balance is already zero. 
                    // no balance change will vialate the rule of send
                    // so we reduce the balance of LYR, or the collateral, of 0.00000001
                    if (b.Balances[orderb.Order.offering] != 0)
                        b.Balances[orderb.Order.offering] = 0;

                    b.Balances["LYR"] = 0;          // all remaining LYR
                });

            return blockNext;
        }

        //protected async Task<TransactionBlock> SendCollateralToSellerAsync(DagSystem sys, LyraContext context)
        //{
        //    var send = context.Send;
        //    var daoid = send.Tags["daoid"];
        //    var orderid = send.Tags["orderid"];
        //    return await SendCollateralToSellerAsync(sys, context, send.Hash, orderid);
        //}

        // send collateral and offering token back to seller
        // close the order
        //protected async Task<TransactionBlock> SealUniOrderAsync(DagSystem sys, LyraContext context, string wfkey, string orderid)
        //{

        //}

        /*        protected async Task<TransactionBlock> SendCollateralToSellerAsync(DagSystem sys, LyraContext context, string wfkey, string orderid)
                {
                    var orderlatest = await sys.Storage.FindLatestBlockAsync(orderid) as TransactionBlock;
                    var daoid = (orderlatest as IUniOrder).Order.daoId;
                    var daolastblock = await sys.Storage.FindLatestBlockAsync(daoid) as TransactionBlock;

                    // get dao for order genesis
                    var odrgen = await sys.Storage.FindFirstBlockAsync(orderid) as ReceiveTransferBlock;
                    var daoforodr = await sys.Storage.FindBlockByHashAsync(odrgen.SourceHash) as IDao;
                    var order = (odrgen as IUniOrder).Order;

                    // about the calculation of fee.
                    // first we need fee paid by LYR. so we need the value of the token.
                    // but we don't know the value of token.
                    // but we know that the buyer/sell know the value.
                    // so we have the fee based on the recognize of buyer/seller.
                    // collateral by the seller/buyer rito, and calculate it by 100%.
                    // the 100% of value is maintained by both the seller and buyer. no more, no less.

                    // order owner's fee is calculated on order close.
                    // trade owner's fee is calculated on trade close.
                    // calculate fees
                    // dao fee + network fee
                    decimal totalFee = 0;
                    decimal networkFee = 0;

                    var allTrades = await sys.Storage.FindUniTradeForOrderAsync(orderid);
                    var totalAmount = allTrades.Cast<IUniTrade>()
                        .Where(a => a.UTStatus == UniTradeStatus.Closed)
                        .Sum(a => a.Trade.amount);

                    // transaction fee
                    totalFee += Math.Round(order.cltamt * daoforodr.SellerFeeRatio, 8);
                    networkFee = order.cltamt * LyraGlobal.OfferingNetworkFeeRatio;

                    Console.WriteLine($"Seller collateral {order.cltamt} paid svc {totalFee} LYR net fee {networkFee} LYR.");
                    var amountToSeller = order.cltamt - totalFee;

                    //Console.WriteLine($"collateral: {order.collateral} txfee: {totalFee} netfee: {networkFee} remains: {order.collateral - totalFee - networkFee} cost: {totalFee + networkFee}");

                    var sb = await sys.Storage.GetLastServiceBlockAsync();
                    var sendCollateral = new DaoSendBlock
                    {
                        // block
                        ServiceHash = sb.Hash,

                        // trans
                        Fee = networkFee,
                        FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                        FeeType = AuthorizationFeeTypes.Dynamic,
                        AccountID = daolastblock.AccountID,

                        // send
                        DestinationAccountId = (orderlatest as IBrokerAccount).OwnerAccountId,

                        // broker
                        Name = ((IBrokerAccount)daolastblock).Name,
                        OwnerAccountId = ((IBrokerAccount)daolastblock).OwnerAccountId,
                        RelatedTx = wfkey,

                        // profiting
                        PType = ((IProfiting)daolastblock).PType,
                        ShareRito = ((IProfiting)daolastblock).ShareRito,
                        Seats = ((IProfiting)daolastblock).Seats,

                        // dao
                        SellerFeeRatio = ((IDao)daolastblock).SellerFeeRatio,
                        BuyerFeeRatio = ((IDao)daolastblock).BuyerFeeRatio,
                        SellerPar = ((IDao)daolastblock).SellerPar,
                        BuyerPar = ((IDao)daolastblock).BuyerPar,
                        Description = ((IDao)daolastblock).Description,
                        Treasure = ((IDao)daolastblock).Treasure.ToDecimalDict().ToLongDict(),
                    };

                    // calculate balance
                    var dict = daolastblock.Balances.ToDecimalDict();
                    dict[LyraGlobal.OFFICIALTICKERCODE] -= amountToSeller;
                    sendCollateral.Balances = dict.ToLongDict();

                    sendCollateral.AddTag(Block.MANAGEDTAG, context.State.ToString());

                    sendCollateral.InitializeBlock(daolastblock, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
                    return sendCollateral;
                }*/
    }
}
