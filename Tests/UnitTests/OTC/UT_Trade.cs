using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.API.Identity;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests.OTC
{
    [TestClass]
    public class UT_Trade : XOTCTestBase
    {
        public async Task Setup()
        {
            await SetupWallets("devnet");

            await SetupEventsListener();
        }

        [TestMethod]
        public async Task TestCancelling()
        {
            await Setup();

            var order = await CreateOrder();
            Assert.IsNotNull(order);

            var trade = await CreateTrade(order);
            Assert.IsNotNull(trade);

            await CancelTrade(trade);

            await CloseOrder(order);
        }

        [TestMethod]
        public async Task TestDisputeRaise()
        {
            await Setup();

            var order = await CreateOrder();
            Assert.IsNotNull(order);

            var trade = await CreateTrade(order);
            Assert.IsNotNull(trade);

            var lsb = await client.GetLastServiceBlockAsync();
            var brief0 = await GetBrief(lsb.GetBlock().Hash, trade.AccountID);
            Assert.AreEqual(trade.AccountID, brief0.TradeId);
            Assert.AreEqual(DisputeLevels.None, brief0.DisputeLevel);

            var ret = await dealer.ComplainAsync(trade.AccountID, trade.Trade.collateral, test2PublicKey,
                Signatures.GetSignature(test2PrivateKey, lsb.GetBlock().Hash, test2PublicKey)                
                );
            Assert.IsTrue(ret.Successful());

            var brief1 = await GetBrief(lsb.GetBlock().Hash, trade.AccountID);
            Assert.AreEqual(trade.AccountID, brief1.TradeId);
            Assert.AreEqual(DisputeLevels.Peer, brief1.DisputeLevel);

            await CancelTradeShouldFail(trade);

            await CloseOrderShouldFail(order);
        }

        private async Task<TradeBrief> GetBrief(string hash, string tradeId)
        {            
            var briefret = await dealer.GetTradeBriefAsync(tradeId, test2PublicKey,
                Signatures.GetSignature(test2PrivateKey, hash, test2PublicKey));
            Assert.IsTrue(briefret.Successful());
            var brief = briefret.Deserialize<TradeBrief>();
            return brief;
        }
    }
}
