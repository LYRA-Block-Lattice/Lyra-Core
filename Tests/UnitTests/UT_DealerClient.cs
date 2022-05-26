using Lyra.Core.API;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
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

            var tradid = "L8FufT33KuUrAsHYgCC1zo4gVRdZA8WBS7SMM8Tevq74B4N4wemz5mBh8CqCmgS3vKa4TiTsVH5kMi49Gn4962hfn8JcT7";

            var cfg = new CommentConfig();
            cfg.TradeId = tradid;
            cfg.Content = "hahaha";
            cfg.Title = "title";

            cfg.ByAccountId = pub;
            cfg.Created = DateTime.UtcNow;

            cfg.EncContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(cfg.Content));
            cfg.EncTitle = Convert.ToBase64String(Encoding.UTF8.GetBytes(cfg.Title));

            cfg.Sign(pvt, pub);

            //var result = await dealer.CommentTradeAsync(cfg);
            //Assert.IsTrue(result.Successful(), $"comment failed: {result.ResultCode}");

            //var cmnts = await dealer.GetCommentsForTradeAsync(tradid);
            //Assert.IsTrue(cmnts.Count == 1, $"no comment found.");
            //Assert.IsTrue(cmnts.First().Content == cfg.Content);
        }
    }
}
