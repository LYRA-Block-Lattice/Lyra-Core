﻿using Lyra.Core.API;
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
    public class UniSharedWFCode : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            throw new Exception("Shared code. Should not call me.");
        }

        public override Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            throw new Exception("Shared code. Should not call me.");
        }

        protected async Task<TransactionBlock> SealOrderAsync(DagSystem sys, SendTransferBlock send)
        {
            var daoid = send.Tags["daoid"];
            var orderid = send.Tags["orderid"];
            var lastblock = await sys.Storage.FindLatestBlockAsync(orderid) as TransactionBlock;

            var blockNext = await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<UniOrderSendBlock>(),
                () => WFState.Running,
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

        protected async Task<TransactionBlock> SendCollateralToSellerAsync(DagSystem sys, SendTransferBlock send)
        {
            var daoid = send.Tags["daoid"];
            var orderid = send.Tags["orderid"];
            var orderlatest = await sys.Storage.FindLatestBlockAsync(orderid) as TransactionBlock;
            var daolastblock = await sys.Storage.FindLatestBlockAsync(daoid) as TransactionBlock;

            // get dao for order genesis
            var odrgen = await sys.Storage.FindFirstBlockAsync(orderid) as ReceiveTransferBlock;
            var daoforodr = await sys.Storage.FindBlockByHashAsync(odrgen.SourceHash) as IDao;
            var order = (odrgen as IUniOrder).Order;

            // order owner's fee is calculated on order close.
            // trade owner's fee is calculated on trade close.
            // calculate fees
            // dao fee + network fee
            decimal totalFee = 0;
            decimal networkFee = 0;

            var allTrades = await sys.Storage.FindUniTradeForOrderAsync(orderid);
            var totalAmount = allTrades.Cast<IUniTrade>()
                .Where(a => a.UTStatus == UniTradeStatus.OfferReceived)
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
                RelatedTx = send.Hash,

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

            sendCollateral.AddTag(Block.MANAGEDTAG, WFState.Finished.ToString());

            sendCollateral.InitializeBlock(daolastblock, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return sendCollateral;
        }
    }
}