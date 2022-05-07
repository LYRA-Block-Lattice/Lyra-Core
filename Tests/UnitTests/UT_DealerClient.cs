using Lyra.Core.API;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_DealerClient
    {
        private string networkId = TestConfig.networkId;

        [TestMethod]
        public async Task TestGetPriceAsync()
        {
            var client = new DealerClient(networkId);

            var prices = await client.GetPricesAsync();

            Assert.IsTrue(prices.ContainsKey("LYR"));
            Assert.IsTrue(prices.ContainsKey("BTC"));
            Assert.IsTrue(prices.ContainsKey("LYR_INT"));
        }


        [TestMethod]
        public async Task TestGetFiatAsync()
        {
            var client = new DealerClient(networkId);

            var usd = await client.GetFiatAsync("USD");
            Assert.IsNotNull(usd);
            Assert.AreEqual("US Dollar", usd.name);

            var ddd = await client.GetFiatAsync("DDD");
            Assert.IsNull(ddd);


        }

        [TestMethod]
        public async Task TestCommentAsync()
        {
            var dealer = new DealerClient(networkId);
            var (pvt, pub) = Signatures.GenerateWallet();

            var cfg = new CommentConfig();
            cfg.TradeId = pub;
            cfg.Content = "hahaha";
            cfg.Title = "title";

            cfg.ByAccountId = pub;
            cfg.Created = DateTime.UtcNow;
            cfg.Sign(pvt, pub);

            var result = await dealer.CommentTrade(cfg);
            Assert.IsTrue(result.Successful());

        }
    }
}
