using Lyra.Core.API;
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
    public class UT_Simple
    {
        [TestMethod]
        public async Task TestGetTradableOtc()
        {
            var lyraApi = LyraRestClient.Create("devnet", "", "", "");
            var tosret = await lyraApi.FindTradableOtcAsync();
            Assert.IsTrue(tosret.Successful());

            var allblks = tosret.GetBlocks("orders");
            var tradableOrders = allblks.Cast<IOtcOrder>()
                .Where(a => a.Order.amount > 0)
                .OrderBy(a => a.Order.price)
                .ToList();
            var tradableCryptos = tradableOrders.Select(a => a.Order.crypto)
                .Distinct()
                .ToList();

            var daos = tosret.GetBlocks("daos").Cast<IDao>().ToList();
        }

        [TestMethod]
        public async Task TestOtcUserStats()
        {
            var lyraApi = LyraRestClient.Create("devnet", "", "", "");

            var req = new TradeStatsReq
            {
                AccountIDs = new List<string>()
                {
                    "LWpgzK3qA8KF4xGc2qQZdiVrsEmynrpdJgFzc6H8H45NDvL6379pqEqAfj2VYkAnt6VeSknj3MFXVm6PdkfXQv8ZdgHmFU",
                    "LBhTYHys8XgYCex6SpvX1cLWjpXs6FUNeGm3Srb3nz12U5x2SQe9hhiah6YWatZG7Py8EB6m45yhFsSMP1tZaJqwSKrrkM"
                }
            };

            var ret = await lyraApi.GetOtcTradeStatsForUsersAsync(req);
            Assert.IsTrue(ret.Successful(), $"failed: {ret.ResultCode}");

            var stats = ret.Deserialize<List<TradeStats>>();
            Assert.AreEqual(2, stats.Count);
            Assert.AreEqual(req.AccountIDs.First(), stats.First().AccountId);
        }
    }
}
