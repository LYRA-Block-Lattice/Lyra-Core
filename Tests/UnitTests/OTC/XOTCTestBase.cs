﻿using Akka.Util;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests.OTC
{
    public class XOTCTestBase : XTestBase
    {
        string dealerID = "L9vh5kuijpaDiqYAaHoV6EejAL3qUXF15JrSR1LvHien3h4fHR3B9p65ubF9AgQnnMzUxdLbDTPtjwpbxB5SPPtSaF4wMr";

        // testWallet is host, testWallet2 is guest.

        //[TestMethod]
        //public async Task TestODR()
        //{
        //    await SetupWallets("devnet");

        //    await SetupEventsListener();

        //    var order = await CreateOrder();
        //    Assert.IsNotNull(order);

        //    var trade = await CreateTrade(order);
        //    Assert.IsNotNull(trade);

        //    await CancelTrade(trade);

        //    await CloseOrder(order);
        //}

        protected async Task GuestCancelTradeAsync(IOtcTrade trade)
        {
            var cloret = await test2Wallet.CancelOTCTradeAsync(trade.Trade.daoId, trade.Trade.orderId, trade.AccountID);
            Assert.IsTrue(cloret.Successful(), $"Error cancel trade: {cloret.ResultCode}");
            await WaitWorkflow(cloret.TxHash, "CancelOTCTradeAsync", APIResultCodes.Success);

            var tradeQueryRet = await test2Wallet.RPC.FindOtcTradeAsync(test2Wallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet.Successful(), $"Can't query trade via FindOtcTradeAsync: {tradeQueryRet.ResultCode}");
            var trades = tradeQueryRet.GetBlocks();

            var lastTrade = trades.Last() as IOtcTrade;
            Assert.IsTrue(trade.AccountID == lastTrade.AccountID && lastTrade.OTStatus == OTCTradeStatus.Canceled);

            //Assert.IsTrue(!trades.Any(a => (a as IOtcTrade).OTStatus == OTCTradeStatus.Open), $"real state is: {}");
        }

        protected async Task CancelTradeShouldFail(IOtcTrade trade)
        {
            var cloret = await test2Wallet.CancelOTCTradeAsync(trade.Trade.daoId, trade.Trade.orderId, trade.AccountID);
            Assert.IsTrue(cloret.Successful(), $"Error cancel trade: {cloret.ResultCode}");
            Assert.IsFalse(test2Wallet.WaitForWorkflow(cloret.TxHash, 3000));
            Assert.IsTrue(test2Wallet.IsLastWorkflowRefund, "cancel trade should fail");
        }

        protected async Task<IOtcTrade> GuestCreateTrade(IOtcOrder order)
        {
            var prices = await dealer.GetPricesAsync();
            var collt = Math.Round((prices["BTC"] * 0.01m / prices["LYR"]) * 150 / 100 + 10000, 0);
            var trade = new OTCTrade
            {
                daoId = order.Order.daoId,
                dealerId = order.Order.dealerId,
                orderId = order.AccountID,
                orderOwnerId = order.OwnerAccountId,
                dir = order.Order.dir == TradeDirection.Sell ? TradeDirection.Buy : TradeDirection.Sell,
                crypto = "tether/BTC",
                fiat = fiat,
                price = order.Order.price,

                collateral = collt,
                payVia = "Paypal",
                amount = 0.01m,
                pay = order.Order.price * 0.01m,
            };

            var traderet = await test2Wallet.CreateOTCTradeAsync(trade);
            Assert.IsTrue(traderet.Successful(), $"OTC Trade error: {traderet.ResultCode}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(traderet.TxHash), "No TxHash for trade create.");

            await WaitWorkflow(traderet.TxHash, $"Create trade, hash {traderet.TxHash}");

            var tradeQueryRet = await test2Wallet.RPC.FindOtcTradeAsync(test2Wallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet.Successful(), $"Can't query trade via FindOtcTradeAsync: {tradeQueryRet.ResultCode}");
            var tradeQueryResultBlocks = tradeQueryRet.GetBlocks();

            var itrade = tradeQueryResultBlocks
                .OrderBy(a => a.TimeStamp)
                .Last() as IOtcTrade;

            Assert.AreEqual(traderet.TxHash, itrade.RelatedTx);
            Assert.AreEqual(OTCTradeStatus.Open, itrade.OTStatus);
            return itrade;
        }

        protected async Task HostCloseOrder(IOtcOrder order)
        {
            var closeret = await testWallet.CloseOTCOrderAsync(order.Order.daoId, order.AccountID);
            Assert.IsTrue(closeret.Successful(), $"Unable to close order: {closeret.ResultCode}");

            await WaitWorkflow(closeret.TxHash, "Close order");
        }

        protected async Task CloseOrderShouldFail(IOtcOrder order)
        {
            var closeret = await testWallet.CloseOTCOrderAsync(order.Order.daoId, order.AccountID);
            Assert.IsTrue(!closeret.Successful(), $"Should fail to close order: {closeret.ResultCode}");
        }

        protected async Task<IOtcOrder> HostCreateOrder()
        {
            var crypto = "tether/BTC";

            await testWallet.SyncAsync(null);
            if (!testWallet.GetLastSyncBlock().Balances.ContainsKey(crypto))
            {
                // init. create token to sell
                var tokenGenesisResult = await testWallet.CreateTokenAsync("BTC", "tether", "", 8, 100000, false, testWallet.AccountId,
                        "", "", ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(tokenGenesisResult.Successful(), $"test otc token genesis failed: {tokenGenesisResult.ResultCode} for {testWallet.AccountId}");

                await testWallet.SyncAsync(null);

                await testWallet.SendAsync(100, test2PublicKey, crypto);
                await test2Wallet.SyncAsync(null);
            }

            Assert.IsTrue(testWallet.GetLastSyncBlock().Balances.ContainsKey(crypto));

            // first create a DAO
            var name = "Test4's DAO";
            var desc = "Doing great business!";
            var daoret = await test4Wallet.RPC.GetDaoByNameAsync(name);
            if (!daoret.Successful())
            {
                var dcret = await test4Wallet.CreateDAOAsync(name, desc, 1, 0.01m, 0.001m, 10, 120, 130);
                Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");
                await Task.Delay(3000);// wait it to be created.
            }
            var dao = daoret.GetBlock() as IDao;
            Assert.AreEqual(name, dao.Name);

            if(dao.Treasure.Count == 0)
            {
                // the dao should have at least one staking to let vote doable. so test3 should be the staker
                var invret4 = await test3Wallet.JoinDAOAsync(dao.AccountID, 50000m);
                Assert.IsTrue(invret4.Successful());
            }

            var prices = await dealer.GetPricesAsync();

            var collt = Math.Round((prices["BTC"] * 0.02m / prices["LYR"]) * dao.SellerPar / 100 + 10000, 0);
            var order = new OTCOrder
            {
                daoId = dao.AccountID,
                dealerId = dealerID,
                dir = TradeDirection.Sell,
                crypto = crypto,
                fiat = fiat,
                fiatPrice = prices[fiat.ToLower()],
                priceType = PriceType.Fixed,
                price = 2,
                collateral = collt,
                collateralPrice = prices["LYR"],
                payBy = new string[] { "Paypal" },

                amount = 0.02m,
                limitMin = 0.01m,
                limitMax = 0.02m,
            };

            var dt = DateTime.UtcNow;
            var ret = await testWallet.CreateOTCOrderAsync(order);
            Assert.IsTrue(ret.Successful(), $"can't create order: {ret.ResultCode}");

            Console.WriteLine($"Send Hash is {ret.TxHash}");
            await WaitWorkflow(ret.TxHash, "create order");
            await Task.Delay(1000);

            var otcret = await testWallet.RPC.GetOtcOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(otcret.Successful(), $"Can't get otc gensis block. {otcret.ResultCode}");
            var otcs = otcret.GetBlocks();
            var odr = otcs.Last();
            Assert.IsTrue(odr.TimeStamp >= dt, "not get last order properly");
            return odr as IOtcOrder;
        }
    }
}
