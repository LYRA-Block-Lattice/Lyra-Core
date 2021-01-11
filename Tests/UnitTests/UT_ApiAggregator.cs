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
        private string networkId = "devnet";

        [TestMethod]
        public async Task TestGetServiceBlockAsync()
        {
            var client = LyraRestClient.Create(networkId, "Windows", "UnitTest", "1.0");

            var aggClient = new LyraAggregatedClient(networkId);
            await aggClient.InitAsync();

            var svcBlock1 = await client.GetLastServiceBlock();
            var svcBlock2 = await aggClient.GetLastServiceBlock();

            Assert.AreEqual(svcBlock1, svcBlock2);
        }

        [TestMethod]
        public async Task TestGetLastBlockAsync()
        {
            var client = LyraRestClient.Create(networkId, "Windows", "UnitTest", "1.0");

            var aggClient = new LyraAggregatedClient("testnet");
            await aggClient.InitAsync();

            var accountId = "LT8din6wm6SyfnqmmJN7jSnyrQjqAaRmixe2kKtTY4xpDBRtTxBmuHkJU9iMru5yqcNyL3Q21KDvHK45rkUS4f8tkXBBS3";
            var svcBlock1 = await client.GetLastBlock(accountId);
            var svcBlock2 = await aggClient.GetLastBlock(accountId);

            Assert.AreEqual(svcBlock1, svcBlock2);
        }

        [TestMethod]
        public async Task TestGetFee()
        {
            var client = LyraRestClient.Create(networkId, "Windows", "UnitTest", "1.0");

            var aggClient = new LyraAggregatedClient(networkId);
            await aggClient.InitAsync();

            var svcBlock1 = await client.GetFeeStatsAsync();
            var svcBlock2 = aggClient.GetFeeStats();

            Assert.AreEqual(svcBlock1, svcBlock2);
        }

    }
}
