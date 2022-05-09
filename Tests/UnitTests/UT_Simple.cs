using Lyra.Core.API;
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
    }
}
