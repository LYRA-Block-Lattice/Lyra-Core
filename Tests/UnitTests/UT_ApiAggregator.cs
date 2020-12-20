using Lyra.Core.API;
using Lyra.Data.API;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace UnitTests
{
    [TestClass]
    public class UT_ApiAggregator
    {
        [TestMethod]
        public async System.Threading.Tasks.Task TestGetServiceBlockAsync()
        {
            var client = LyraRestClient.Create("testnet", "Windows", "UnitTest", "1.0");

            var aggClient = new LyraAggregatedClient("testnet");
            await aggClient.InitAsync("api.testnet.lyra.live");

            var svcBlock1 = await client.GetLastServiceBlock();
            var svcBlock2 = await aggClient.GetLastServiceBlock();

            Assert.AreEqual(svcBlock1, svcBlock2);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task TestGetLastBlockAsync()
        {
            var client = LyraRestClient.Create("testnet", "Windows", "UnitTest", "1.0");

            var aggClient = new LyraAggregatedClient("testnet");
            await aggClient.InitAsync("api.testnet.lyra.live");

            var accountId = "LT8din6wm6SyfnqmmJN7jSnyrQjqAaRmixe2kKtTY4xpDBRtTxBmuHkJU9iMru5yqcNyL3Q21KDvHK45rkUS4f8tkXBBS3";
            var svcBlock1 = await client.GetLastBlock(accountId);
            var svcBlock2 = await aggClient.GetLastBlock(accountId);

            Assert.AreEqual(svcBlock1, svcBlock2);
        }
    }
}
