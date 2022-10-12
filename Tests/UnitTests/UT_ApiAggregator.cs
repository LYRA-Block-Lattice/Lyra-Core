using Lyra.Core.API;
using Lyra.Data.API;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_ApiAggregator
    {
        private string networkId = TestConfig.networkId;

        [TestMethod]
        public async Task TestGetServiceBlockAsync()
        {
            var client = LyraRestClient.Create(networkId, "Windows", "UnitTest", "1.0");

            var bb = await client.GetBillBoardAsync();
            var aggClient = new LyraAggregatedClient(networkId, false, null, bb);

            var svcBlock1 = await client.GetLastServiceBlockAsync();
            var svcBlock2 = await aggClient.GetLastServiceBlockAsync();

            Assert.AreEqual(svcBlock1, svcBlock2);
        }

        //[TestMethod]
        //public async Task TestGetLastBlockAsync()
        //{
        //    var client = LyraRestClient.Create(networkId, "Windows", "UnitTest", "1.0");

        //    var aggClient = new LyraAggregatedClient(networkId, false);
        //    await aggClient.InitAsync();

        //    var accountId = "LT8din6wm6SyfnqmmJN7jSnyrQjqAaRmixe2kKtTY4xpDBRtTxBmuHkJU9iMru5yqcNyL3Q21KDvHK45rkUS4f8tkXBBS3";
        //    var svcBlock1 = await client.GetLastBlockAsync(accountId);
        //    var svcBlock2 = await aggClient.GetLastBlockAsync(accountId);

        //    Assert.AreEqual(svcBlock1.GetBlock().Hash, svcBlock2.GetBlock().Hash);
        //}

        //[TestMethod]
        //public async Task TestGetFeeAsync()
        //{
        //    var client = LyraRestClient.Create(networkId, "Windows", "UnitTest", "1.0");

        //    var aggClient = new LyraAggregatedClient(networkId, false);
        //    await aggClient.InitAsync();

        //    var svcBlock1 = await client.GetFeeStatsAsync();
        //    var svcBlock2 = aggClient.GetFeeStats();

        //    Assert.AreEqual(svcBlock1, svcBlock2);
        //}

    }
}
