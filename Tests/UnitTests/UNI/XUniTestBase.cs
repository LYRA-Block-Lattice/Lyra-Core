using Akka.Util;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests.Uni
{
    public class XUniTestBase : XTestBase
    {
        protected string dealerID = "L9vh5kuijpaDiqYAaHoV6EejAL3qUXF15JrSR1LvHien3h4fHR3B9p65ubF9AgQnnMzUxdLbDTPtjwpbxB5SPPtSaF4wMr";

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

        protected async Task GuestCancelTradeAsync(IUniTrade trade)
        {
            var cloret = await test2Wallet.CancelUniTradeAsync(trade.Trade.daoId, trade.Trade.orderId, trade.AccountID);
            Assert.IsTrue(cloret.Successful(), $"Error cancel trade: {cloret.ResultCode}");
            await WaitWorkflow(cloret.TxHash, "CancelUniTradeAsync", APIResultCodes.Success);

            var tradeQueryRet = await test2Wallet.RPC.FindUniTradeAsync(test2Wallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet.Successful(), $"Can't query trade via FindUniTradeAsync: {tradeQueryRet.ResultCode}");
            var trades = tradeQueryRet.GetBlocks();

            var lastTrade = trades.Last() as IUniTrade;
            Assert.IsTrue(trade.AccountID == lastTrade.AccountID && lastTrade.UTStatus == UniTradeStatus.Canceled);

            //Assert.IsTrue(!trades.Any(a => (a as IUniTrade).OTStatus == UniTradeStatus.Open), $"real state is: {}");
        }

        protected async Task CancelTradeShouldFail(IUniTrade trade)
        {
            var cloret = await test2Wallet.CancelUniTradeAsync(trade.Trade.daoId, trade.Trade.orderId, trade.AccountID);
            Assert.IsTrue(cloret.Successful(), $"Error cancel trade: {cloret.ResultCode}");
            Assert.IsFalse(test2Wallet.WaitForWorkflow(cloret.TxHash, 3000));
            Assert.IsTrue(test2Wallet.IsLastWorkflowRefund, "cancel trade should fail");
        }

        protected async Task<IUniTrade> GuestCreateTrade(IUniOrder order)
        {
            //var prices = await dealer.GetPricesAsync();
            var collt = 10000;// Math.Round((prices["BTC"] * 0.01m / prices["LYR"]) * 150 / 100 + 10000, 0);
            var trade = new UniTrade
            {
                daoId = order.Order.daoId,
                dealerId = order.Order.dealerId,
                orderId = order.AccountID,
                orderOwnerId = order.OwnerAccountId,
                offering = "tether/BTC",
                offby = HoldTypes.Token,
                biding = fiat,
                bidby = HoldTypes.Fiat,
                price = order.Order.price,

                cltamt = collt,
                payVia = "Paypal",
                amount = 0.01m,
                pay = order.Order.price * 0.01m,
            };

            var traderet = await test2Wallet.CreateUniTradeAsync(trade);
            Assert.IsTrue(traderet.Successful(), $"Uni Trade error: {traderet.ResultCode}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(traderet.TxHash), "No TxHash for trade create.");

            await WaitWorkflow(traderet.TxHash, $"Create trade, hash {traderet.TxHash}");

            var tradeQueryRet = await test2Wallet.RPC.FindUniTradeAsync(test2Wallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet.Successful(), $"Can't query trade via FindUniTradeAsync: {tradeQueryRet.ResultCode}");
            var tradeQueryResultBlocks = tradeQueryRet.GetBlocks();

            var itrade = tradeQueryResultBlocks
                .OrderBy(a => a.TimeStamp)
                .Last() as IUniTrade;

            Assert.AreEqual(traderet.TxHash, itrade.RelatedTx);
            Assert.AreEqual(UniTradeStatus.Open, itrade.UTStatus);
            return itrade;
        }

        protected async Task HostCloseOrder(IUniOrder order)
        {
            var closeret = await testWallet.CloseUniOrderAsync(order.Order.daoId, order.AccountID);
            Assert.IsTrue(closeret.Successful(), $"Unable to close order: {closeret.ResultCode}");

            await WaitWorkflow(closeret.TxHash, "Close order");
        }

        protected async Task CloseOrderShouldFail(IUniOrder order)
        {
            var closeret = await testWallet.CloseUniOrderAsync(order.Order.daoId, order.AccountID);
            Assert.IsTrue(!closeret.Successful(), $"Should fail to close order: {closeret.ResultCode}");
        }

        protected async Task<IUniOrder> HostCreateOrder()
        {
            var crypto = "tether/BTC";

            await testWallet.SyncAsync(null);
            if (null == await testWallet.GetTokenGenesisBlockAsync(crypto))
            {
                // init. create token to sell
                var tokenGenesisResult = await genesisWallet.CreateTokenAsync("BTC", "tether", "", 8, 100000, false, testWallet.AccountId,
                        "", "", ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(tokenGenesisResult.Successful(), $"test Uni token genesis failed: {tokenGenesisResult.ResultCode} for {testWallet.AccountId}");

                await genesisWallet.SyncAsync(null);

                await genesisWallet.SendAsync(100, test2PublicKey, crypto);
                await test2Wallet.SyncAsync(null);
            }

            if(!testWallet.GetLastSyncBlock().Balances.ContainsKey(crypto))
            {
                await genesisWallet.SendAsync(100, testPublicKey, crypto);
                await testWallet.SyncAsync(null);
            }

            if(test4Wallet.BaseBalance < 1000000)
            {
                await genesisWallet.SendAsync(1000000, test4PublicKey);
                await test4Wallet.SyncAsync(null);
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

            //var prices = await dealer.GetPricesAsync();

            var collt = 10000; //Math.Round((prices["BTC"] * 0.02m / prices["LYR"]) * dao.SellerPar / 100 + 10000, 0);
            var order = new UniOrder
            {
                daoId = dao.AccountID,
                dealerId = dealerID,
                offering = crypto,
                offerby = HoldTypes.Token,
                biding = fiat,
                bidby = HoldTypes.Fiat,
                price = 2,
                cltamt = collt,
                payBy = new string[] { "Paypal" },

                amount = 0.02m,
                limitMin = 0.01m,
                limitMax = 0.02m,
            };

            var dt = DateTime.UtcNow;
            var ret = await testWallet.CreateUniOrderAsync(order);
            Assert.IsTrue(ret.Successful(), $"can't create order: {ret.ResultCode}");

            Console.WriteLine($"Send Hash is {ret.TxHash}");
            await WaitWorkflow(ret.TxHash, "create order");
            await Task.Delay(1000);

            var Uniret = await testWallet.RPC.GetUniOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(Uniret.Successful(), $"Can't get Uni gensis block. {Uniret.ResultCode}");
            var Unis = Uniret.GetBlocks();
            var odr = Unis.Last();
            Assert.IsTrue(odr.TimeStamp >= dt, "not get last order properly");
            return odr as IUniOrder;
        }
    }
}
