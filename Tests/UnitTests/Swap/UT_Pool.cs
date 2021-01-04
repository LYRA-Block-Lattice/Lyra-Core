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
        private string testTokenA = "unittest/PoolCoinB1";

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

            // make sure we have 2 test token
            var genResult = await client.GetTokenGenesisBlock(testPublicKey, testTokenA, await SignAPIAsync());
            if(genResult.ResultCode == APIResultCodes.TokenGenesisBlockNotFound)
            {
                var secs = testTokenA.Split('/');
                var result = await w1.CreateToken(secs[1], secs[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(result.Successful(), "Failed to create token: " + result.ResultCode);
                await w1.Sync(client);
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

            await w1.Sync(client);
            Assert.IsTrue(balances[testTokenA].ToBalanceDecimal() > 100000m, "Insufficient funds: " + testTokenA);
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

                var swapRito = 0m;
                var poolLatestBlock = pool.GetBlock() as TransactionBlock;
                if (poolLatestBlock.Balances.ContainsKey(pool.Token0))
                    swapRito = poolLatestBlock.Balances[pool.Token0].ToBalanceDecimal() / poolLatestBlock.Balances[pool.Token1].ToBalanceDecimal();

                if (swapRito == 0)
                {
                    var result = await w1.AddLiquidateToPoolAsync(pool.Token0, 50000m, pool.Token1, 3000000m);
                    Assert.IsTrue(result.ResultCode == APIResultCodes.Success, "Unable to deposit to pool: " + result.ResultCode);
                }
                else
                {
                    var token0Amount = (decimal)((new Random().NextDouble() + 0.03) * 1000);
                    var token1Amount = Math.Round(token0Amount / swapRito, 8);

                    var result = await w1.AddLiquidateToPoolAsync(pool.Token0, token0Amount, pool.Token1, token1Amount);
                    Assert.IsTrue(result.ResultCode == APIResultCodes.Success, "Unable to deposit to pool: " + result.ResultCode);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        [TestMethod]
        public async Task PoolWithdraw()
        {
            try
            {
                await semaphore.WaitAsync();

                var pool = await client.GetPool(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);
                Assert.IsTrue(LyraGlobal.OFFICIALTICKERCODE == pool.Token0);
                Assert.IsTrue(testTokenA == pool.Token1);

                var w1 = Restore(testPrivateKey);
                await w1.Sync(client);

                var poolLatest = pool.GetBlock() as TransactionBlock;
                Assert.IsNotNull(poolLatest);

                var poolWithShare = poolLatest as IPool;
                Assert.IsNotNull(poolWithShare);

                if (!poolWithShare.Shares.ContainsKey(w1.AccountId))
                    return;

                var token0BalanceBefore = w1.GetLatestBlock().Balances[pool.Token0].ToBalanceDecimal();
                var token1BalanceBefore = w1.GetLatestBlock().Balances[pool.Token1].ToBalanceDecimal();
                var myshare = poolWithShare.Shares[w1.AccountId].ToRitoDecimal();
                var token0ShouldReceive = Math.Round(myshare * poolLatest.Balances[pool.Token0].ToBalanceDecimal(), 8);
                var token1ShouldReceive = Math.Round(myshare * poolLatest.Balances[pool.Token1].ToBalanceDecimal(), 8);

                var result = await w1.RemoveLiquidateFromPoolAsync(pool.Token0, pool.Token1);
                Assert.IsTrue(result.ResultCode == APIResultCodes.Success, "Unable to withdraw from pool: " + result.ResultCode);

                await Task.Delay(3000);
                pool = await client.GetPool(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);

                poolLatest = pool.GetBlock() as TransactionBlock;
                Assert.IsNotNull(poolLatest);

                poolWithShare = poolLatest as IPool;
                Assert.IsNotNull(poolWithShare);

                Assert.IsFalse(poolWithShare.Shares.ContainsKey(w1.AccountId), "The pool share is still there.");

                await w1.Sync(client);
                Assert.AreEqual(token0BalanceBefore + token0ShouldReceive, w1.GetLatestBlock().Balances[pool.Token0].ToBalanceDecimal());
                Assert.AreEqual(token1BalanceBefore + token1ShouldReceive, w1.GetLatestBlock().Balances[pool.Token1].ToBalanceDecimal());
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
