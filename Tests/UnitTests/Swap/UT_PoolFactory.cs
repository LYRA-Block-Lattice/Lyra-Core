using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTests.Swap
{
    [TestClass]
    public class UT_PoolFactory
    {
        LyraRestClient client = LyraRestClient.Create("devnet", "Windows", "UnitTest", "1.0");
        private string testTokenA = "unittest/UCoinA";
        private string testTokenB = "UCoinB";

        string testPrivateKey = "bdhSJXkMgbHQJDusDrrP9KLEDE7qYpebcko9ui1xbGWPBw97F";
        string testPublicKey = "LUTGHWizn5EzUeJp7UMhhRkR88tmwpPWX98f4WBvz2qEwPhv46G9VtFdCppPRHnJ6htrDHoLXaHdqaqZSETtekK5YqEd7";

        private SemaphoreSlim semaphore = new SemaphoreSlim(1);      // we need to run tests in serial

        private async Task<string> SignAPIAsync()
        {
            var lsb = await client.GetLastServiceBlock(); 
            return Signatures.GetSignature(testPrivateKey, lsb.GetBlock().Hash, testPublicKey);
        }

        public static Wallet Restore(string privateKey)
        {
            var memStor = new AccountInMemoryStorage();
            try
            {
                Wallet.Create(memStor, "tmpAcct", "", "devnet", privateKey);
                return Wallet.Open(memStor, "tmpAcct", "");
            }
            catch (Exception)
            {
                return null;
            }
        }

        [TestInitialize]
        public async Task UT_PoolFactory_SetupAsync()
        {
            // make sure we have 2 test token
            var genResult = await client.GetTokenGenesisBlock(testPublicKey, testTokenA, await SignAPIAsync());
            if(genResult.ResultCode == APIResultCodes.TokenGenesisBlockNotFound)
            {
                var w1 = Restore(testPrivateKey);
                await w1.Sync(client);
                var result = await w1.CreateToken("UCoinA", "unittest", "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(result.Successful(), "Failed to create token: " + result.ResultCode);
            }
        }
        [TestMethod]
        public async Task GetNullPoolFactory()
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
