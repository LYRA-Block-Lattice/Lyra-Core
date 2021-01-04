using FluentAssertions;
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
    public class UT_Pool
    {
        LyraRestClient client = LyraRestClient.Create("devnet", "Windows", "UnitTest", "1.0");
        private string testTokenA = "unittest/PoolCoinA";
        private string testTokenB = "unittest/PoolCoinB";

        string testPrivateKey = "2LqBaZopCiPjBQ9tbqkqqyo4TSaXHUth3mdMJkhaBbMTf6Mr8u";
        string testPublicKey = "LUTPLGNAP4vTzXh5tWVCmxUBh8zjGTR8PKsfA8E67QohNsd1U6nXPk4Q9jpFKsKfULaaT3hs6YK7WKm57QL5oarx8mZdbM";

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
            var w1 = Restore(testPrivateKey);
            await w1.Sync(client);

            var balances = w1.GetLatestBlock().Balances;
            Assert.IsTrue(balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() > 100000m, "Insufficient funds: LYR");
            Assert.IsTrue(balances[testTokenA].ToBalanceDecimal() > 100000m, "Insufficient funds: " + testTokenA);

            // make sure we have 2 test token
            var genResult = await client.GetTokenGenesisBlock(testPublicKey, testTokenA, await SignAPIAsync());
            if(genResult.ResultCode == APIResultCodes.TokenGenesisBlockNotFound)
            {
                var secs = testTokenA.Split('/');
                var result = await w1.CreateToken(secs[1], secs[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(result.Successful(), "Failed to create token: " + result.ResultCode);
            }

            var pool = await client.GetPool(LyraGlobal.OFFICIALTICKERCODE, testTokenA);
            if(pool.PoolAccountId == null)
            {
                var token0 = LyraGlobal.OFFICIALTICKERCODE;
                var token1 = testTokenA;

                var poolCreateResult = await w1.CreateLiquidatePoolAsync(token0, token1);
                await Task.Delay(3000);     // give consens network time to create it.
                Assert.IsTrue(poolCreateResult.ResultCode == APIResultCodes.Success, "Can't create pool for " + token1);
            }
        }
        [TestMethod]
        public async Task PoolSetupProperly()
        {
            var pool = await client.GetPool(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
            Assert.IsNotNull(pool.PoolAccountId);
            pool.PoolAccountId.Should().StartWith("L");
        }

        [TestMethod]
        public async Task PoolDeposition()
        {
            try
            {
                await semaphore.WaitAsync();

                var pool = await client.GetPool(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);

                var w1 = Restore(testPrivateKey);
                await w1.Sync(client);

                if(pool.SwapRito == 0)
                {
                    var result = await w1.AddLiquidateToPoolAsync(pool.Token0, 50000m, pool.Token1, 3000000m);
                    Assert.IsTrue(result.ResultCode == APIResultCodes.Success, "Unable to deposit to pool: " + result.ResultCode);
                }
                else
                {
                    var token0Amount = (decimal)((new Random().NextDouble() + 0.03) * 1000);
                    var token1Amount = Math.Round(token0Amount / pool.SwapRito, 8);

                    var result = await w1.AddLiquidateToPoolAsync(pool.Token0, token0Amount, pool.Token1, token1Amount);
                    Assert.IsTrue(result.ResultCode == APIResultCodes.Success, "Unable to deposit to pool: " + result.ResultCode);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
