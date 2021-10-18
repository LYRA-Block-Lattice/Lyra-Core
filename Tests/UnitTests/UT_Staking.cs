using FluentAssertions;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_Staking
    {
        readonly string networkId = TestConfig.networkId;
        ILyraAPI client;

        // send 1M
        readonly string testPrivateKey = "2LqBaZopCiPjBQ9tbqkqqyo4TSaXHUth3mdMJkhaBbMTf6Mr8u";
        readonly string testPublicKey = "LUTPLGNAP4vTzXh5tWVCmxUBh8zjGTR8PKsfA8E67QohNsd1U6nXPk4Q9jpFKsKfULaaT3hs6YK7WKm57QL5oarx8mZdbM";

        // send 10K
        readonly string otherAccountPrivateKey = "2XAGksPqMDxeSJVoE562TX7JzmCKna3i7AS9e4ZPmiTKQYATsy";
        //string otherAccountPublicKey = "LUTob2rWpFBZ6r3UxHhDYR8Utj4UDrmf1SFC25RpQxEfZNaA2WHCFtLVmURe1ty4ZNU9gBkCCrSt6ffiXKrRH3z9T3ZdXK";

        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);      // we need to run tests in serial

        private async Task<string> SignAPIAsync()
        {
            var lsb = await client.GetLastServiceBlockAsync(); 
            return Signatures.GetSignature(testPrivateKey, lsb.GetBlock().Hash, testPublicKey);
        }

        public Wallet Restore(string privateKey)
        {
            client = LyraRestClient.Create(networkId, "Windows", "UnitTest", "1.0");

            var memStor = new AccountInMemoryStorage();
            try
            {
                Wallet.Create(memStor, "tmpAcct", "", networkId, privateKey);
                return Wallet.Open(memStor, "tmpAcct", "");
            }
            catch (Exception)
            {
                return null;
            }
        }

        [TestMethod]
        public async Task CreateStakingAccountAsync()
        {
            try
            {
                await semaphore.WaitAsync();

                var w1 = Restore(testPrivateKey);
                var syncResult = await w1.SyncAsync(client);
                Assert.IsTrue(syncResult == APIResultCodes.Success);

                var result = await w1.CreateProfitingAccountAsync(ProfitingType.Node, 0.2m, 10);
                Assert.IsTrue(result.ResultCode == APIResultCodes.Success, $"Result: {result.ResultCode}");

                var pgen = result.GetBlock() as ProfitingGenesisBlock;
                Assert.IsNotNull(pgen);

                //var result2 = await w1.CreateStakingAccountAsync(1000m, testPublicKey);
                //Assert.IsTrue(result2.ResultCode == APIResultCodes.Success, $"Result: {result2.ResultCode}");
            }
            finally
            {
                semaphore.Release();
            }
        }


    }
}
