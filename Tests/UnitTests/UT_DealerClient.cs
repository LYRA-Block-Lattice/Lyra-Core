using Lyra.Core.API;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
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
            var url = "https://dealer.devnet.lyra.live:7070";
            var dealer = new DealerClient(new Uri(new Uri(url), "/api/dealer/"));

            var prices = await dealer.GetPricesAsync();

            Assert.IsTrue(prices.ContainsKey("LYR"));
            Assert.IsTrue(prices.ContainsKey("BTC"));
            Assert.IsTrue(prices.ContainsKey("LYR_INT"));
        }


        [TestMethod]
        public async Task TestGetFiatAsync()
        {
            var url = "https://dealer.devnet.lyra.live:7070";
            var dealer = new DealerClient(new Uri(new Uri(url), "/api/dealer/"));

            var usd = await dealer.GetFiatAsync("USD");
            Assert.IsNotNull(usd);
            Assert.AreEqual("US Dollar", usd.name);

            var ddd = await dealer.GetFiatAsync("DDD");
            Assert.IsNull(ddd);


        }

        [TestMethod]
        public async Task TestCommentAsync()
        {
            var url = "https://dealer.devnet.lyra.live:7070";
            var dealer = new DealerClient(new Uri(new Uri(url), "/api/dealer/"));

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

        [TestMethod]
        public async Task TestUserTrustedAsync()
        {
            var url = "https://dealer.devnet.lyra.live:7070";
            var dealer = new DealerClient(new Uri(new Uri(url), "/api/dealer/"));

            var accountId = "LUTPLGNAP4vTzXh5tWVCmxUBh8zjGTR8PKsfA8E67QohNsd1U6nXPk4Q9jpFKsKfULaaT3hs6YK7WKm57QL5oarx8mZdbM";

            var jsonret = await dealer.GetTrustedUserAsync(accountId);
            Assert.IsTrue(jsonret.Successful(), $"Can't GetTrustedUserAsync: {jsonret.ResultMessage}");

            dynamic ut = JObject.Parse(jsonret.JsonString);
            Assert.IsTrue(ut.EmailVerified == "true");
            Assert.IsTrue(ut.TelegramVerified == "true");
        }
    }
}
