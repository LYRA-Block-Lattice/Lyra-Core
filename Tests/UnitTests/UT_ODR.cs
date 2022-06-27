using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_ODR : XTestBase
    {
        string dealerID = "L9vh5kuijpaDiqYAaHoV6EejAL3qUXF15JrSR1LvHien3h4fHR3B9p65ubF9AgQnnMzUxdLbDTPtjwpbxB5SPPtSaF4wMr";

        [TestMethod]
        public async Task TestODR()
        {
            await SetupWallets("devnet");

            await SetupEventsListener();

            var order = await CreateOrder();
            Assert.IsNotNull(order);

            await CloseOrder(order);
        }

        private async Task CloseOrder(IOtcOrder order)
        {
            var closeret = await testWallet.CloseOTCOrderAsync(order.Order.daoId, order.AccountID);
            Assert.IsTrue(closeret.Successful(), $"Unable to close order: {closeret.ResultCode}");

            await WaitWorkflow(closeret.TxHash, "Close order");
        }

        private async Task<IOtcOrder> CreateOrder()
        {
            var crypto = "unittest/ETH";

            await testWallet.SyncAsync(null);
            if (!testWallet.GetLastSyncBlock().Balances.ContainsKey(crypto))
            {
                // init. create token to sell
                var tokenGenesisResult = await testWallet.CreateTokenAsync("ETH", "unittest", "", 8, 100000, false, testWallet.AccountId,
                        "", "", ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(tokenGenesisResult.Successful(), $"test otc token genesis failed: {tokenGenesisResult.ResultCode} for {testWallet.AccountId}");

                await testWallet.SyncAsync(null);

                await testWallet.SendAsync(100, test2PublicKey, crypto);
                await test2Wallet.SyncAsync(null);
            }

            Assert.IsTrue(testWallet.GetLastSyncBlock().Balances.ContainsKey(crypto));

            // first create a DAO
            var name = "First DAO";
            var desc = "Doing great business!";
            var daoret = await testWallet.RPC.GetDaoByNameAsync(name);
            if (!daoret.Successful())
            {
                var dcret = await testWallet.CreateDAOAsync(name, desc, 1, 0.01m, 0.001m, 10, 120, 130);
                Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");
            }
            var dao = daoret.GetBlock() as IDao;
            Assert.AreEqual(name, dao.Name);

            var url = "https://dealer.devnet.lyra.live:7070";
            var dealer = new DealerClient(new Uri(new Uri(url), "/api/dealer/"));
            var prices = await dealer.GetPricesAsync();

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
                collateral = 25_000_000,
                collateralPrice = prices["LYR"],
                payBy = new string[] { "Paypal" },

                amount = 0.02m,
                limitMin = 0.01m,
                limitMax = 0.02m,
            };

            var ret = await testWallet.CreateOTCOrderAsync(order);
            Assert.IsTrue(ret.Successful(), $"can't create order: {ret.ResultCode}");

            Console.WriteLine($"Send Hash is {ret.TxHash}");
            await WaitWorkflow(ret.TxHash, "create order");
            //await Task.Delay(10000);

            var otcret = await testWallet.RPC.GetOtcOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(otcret.Successful(), $"Can't get otc gensis block. {otcret.ResultCode}");
            var otcs = otcret.GetBlocks();
            return otcs.Last() as IOtcOrder;
        }
    }
}
