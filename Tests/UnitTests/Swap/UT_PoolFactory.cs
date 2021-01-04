using Lyra.Core.API;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests.Swap
{
    [TestClass]
    public class UT_PoolFactory
    {
        LyraRestClient client = LyraRestClient.Create("devnet", "Windows", "UnitTest", "1.0");
        private string testTokenA = "UCoinA";
        private string testTokenB = "UCoinB";

        string testPrivateKey = "bdhSJXkMgbHQJDusDrrP9KLEDE7qYpebcko9ui1xbGWPBw97F";
        string testPublicKey = "LUTGHWizn5EzUeJp7UMhhRkR88tmwpPWX98f4WBvz2qEwPhv46G9VtFdCppPRHnJ6htrDHoLXaHdqaqZSETtekK5YqEd7";

        private async Task<string> SignAPIAsync()
        {
            var lsb = await client.GetLastServiceBlock(); 
            return Signatures.GetSignature(testPrivateKey, lsb.GetBlock().Hash, testPublicKey);
        }

        [TestInitialize]
        public async Task UT_PoolFactory_SetupAsync()
        {
            // make sure we have 2 test token
            var genResult = await client.GetTokenGenesisBlock(testPublicKey, testTokenA, await SignAPIAsync());
            if(genResult.ResultCode != Lyra.Core.Blocks.APIResultCodes.Success)
            {
                
            }
        }
        [TestMethod]
        public async Task GetPoolFactoryAsync()
        {
            var pool = await client.GetPool("test1", "test2");
            Assert.IsNull(pool.PoolAccountId);
            Assert.IsTrue(!string.IsNullOrEmpty(pool.PoolFactoryAccountId), "factory not created");
        }

        [TestMethod]
        public async Task CreatePoolAsync()
        {
            var pool = await client.GetPool("test1", "test2");
            Assert.IsNull(pool.PoolAccountId);
            Assert.IsTrue(!string.IsNullOrEmpty(pool.PoolFactoryAccountId), "factory not created");
        }
    }
}
