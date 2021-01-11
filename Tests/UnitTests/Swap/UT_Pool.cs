using FluentAssertions;
using Lyra.Core.Accounts;
using Lyra.Core.API;
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

namespace UnitTests.Swap
{
    [TestClass]
    public class UT_Pool
    {
        string networkId = "devnet";
        ILyraAPI client;
        private string testTokenA = "unittest/PoolCoinB5";
        private string testTokenB = "unittest/PoolCoinX";

        // send 1M
        string testPrivateKey = "2LqBaZopCiPjBQ9tbqkqqyo4TSaXHUth3mdMJkhaBbMTf6Mr8u";
        string testPublicKey = "LUTPLGNAP4vTzXh5tWVCmxUBh8zjGTR8PKsfA8E67QohNsd1U6nXPk4Q9jpFKsKfULaaT3hs6YK7WKm57QL5oarx8mZdbM";

        // send 10K
        string otherAccountPrivateKey = "2XAGksPqMDxeSJVoE562TX7JzmCKna3i7AS9e4ZPmiTKQYATsy";
        string otherAccountPublicKey = "LUTob2rWpFBZ6r3UxHhDYR8Utj4UDrmf1SFC25RpQxEfZNaA2WHCFtLVmURe1ty4ZNU9gBkCCrSt6ffiXKrRH3z9T3ZdXK";

        private SemaphoreSlim semaphore = new SemaphoreSlim(1);      // we need to run tests in serial

        private async Task<string> SignAPIAsync()
        {
            var lsb = await client.GetLastServiceBlock(); 
            return Signatures.GetSignature(testPrivateKey, lsb.GetBlock().Hash, testPublicKey);
        }

        public Wallet Restore(string privateKey)
        {
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

        [TestInitialize]
        public async Task UT_PoolFactory_SetupAsync()
        {
            //var aggClient = new LyraAggregatedClient(networkId);
            //await aggClient.InitAsync();
            //client = aggClient;
            client = LyraRestClient.Create(networkId, "Windows", "UnitTest", "1.0");

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

            genResult = await client.GetTokenGenesisBlock(testPublicKey, testTokenB, await SignAPIAsync());
            if (genResult.ResultCode == APIResultCodes.TokenGenesisBlockNotFound)
            {
                var secs = testTokenB.Split('/');
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
            balances = w1.GetLatestBlock().Balances;
            Assert.IsTrue(balances[testTokenA].ToBalanceDecimal() > 100000m, "Insufficient funds: " + testTokenA);
        }
        [TestMethod]
        public async Task APoolSetupProperly()
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
                if (poolLatestBlock.Balances.Count == 2 && !poolLatestBlock.Balances.Any(a => a.Value == 0))
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
        public async Task ZPoolWithdraw()
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
                // token0 is lyr, and fee + 1m = 2
                Assert.AreEqual(token0BalanceBefore + token0ShouldReceive - 2, w1.GetLatestBlock().Balances[pool.Token0].ToBalanceDecimal());
                Assert.AreEqual(token1BalanceBefore + token1ShouldReceive, w1.GetLatestBlock().Balances[pool.Token1].ToBalanceDecimal());
            }
            finally
            {
                semaphore.Release();
            }
        }

        [TestMethod]
        public async Task SwapTokenAToLYR()
        {
            try
            {
                await semaphore.WaitAsync();

                var pool = await client.GetPool(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);
                var poolLatestBlock = pool.GetBlock() as TransactionBlock;

                Assert.IsTrue(poolLatestBlock.Balances[pool.Token0] > 0 && poolLatestBlock.Balances[pool.Token1] > 0, "No liquidate in pool.");

                var w1 = Restore(testPrivateKey);
                await w1.Sync(client);

                var testTokenBalance = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalance = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                var swapRito = poolLatestBlock.Balances[pool.Token0].ToBalanceDecimal() / poolLatestBlock.Balances[pool.Token1].ToBalanceDecimal();

                var amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                var result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, testTokenA, amount, swapRito, 0m);
                Assert.IsTrue(result.ResultCode == APIResultCodes.Success, $"Failed to swap {testTokenA}: {result.ResultCode}");
                await Task.Delay(3000);

                var amountToGet = Math.Round(swapRito * amount, 8);
                await w1.Sync(client);

                var testTokenBalance2 = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalance2 = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                Assert.AreEqual(testTokenBalance - amount, testTokenBalance2);
                Assert.AreEqual(lyrBalance - 1 + amountToGet, lyrBalance2);
            }
            finally
            {
                semaphore.Release();
            }
        }

        [TestMethod]
        public async Task SwapLYRToTokenA()
        {
            try
            {
                await semaphore.WaitAsync();

                var pool = await client.GetPool(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);
                var poolLatestBlock = pool.GetBlock() as TransactionBlock;

                Assert.IsTrue(poolLatestBlock.Balances[pool.Token0] > 0 && poolLatestBlock.Balances[pool.Token1] > 0, "No liquidate in pool.");

                var w1 = Restore(testPrivateKey);
                await w1.Sync(client);

                var testTokenBalance = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalance = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                var swapRito = Math.Round(poolLatestBlock.Balances[pool.Token0].ToBalanceDecimal() / poolLatestBlock.Balances[pool.Token1].ToBalanceDecimal(), LyraGlobal.RITOPRECISION);
                // lol convert it like node side
                swapRito = long.Parse(swapRito.ToRitoLong().ToString()).ToRitoDecimal();

                var amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                var result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, swapRito, 0m);
                Assert.IsTrue(result.ResultCode == APIResultCodes.Success, $"Failed to swap {LyraGlobal.OFFICIALTICKERCODE}: {result.ResultCode}");
                await Task.Delay(3000);

                var amountToGet = Math.Round(amount / swapRito, 8);
                await w1.Sync(client);

                var testTokenBalance2 = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalance2 = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                Assert.AreEqual(testTokenBalance + amountToGet, testTokenBalance2);
                Assert.AreEqual(lyrBalance - 1 - amount, lyrBalance2);
            }
            finally
            {
                semaphore.Release();
            }
        }

        [TestMethod]
        public async Task SwapWithSlippage()
        {
            try
            {
                await semaphore.WaitAsync();

                var pool = await client.GetPool(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);
                var poolLatestBlock = pool.GetBlock() as TransactionBlock;

                Assert.IsTrue(poolLatestBlock.Balances[pool.Token0] > 0 && poolLatestBlock.Balances[pool.Token1] > 0, "No liquidate in pool.");

                var w1 = Restore(testPrivateKey);
                await w1.Sync(client);

                var testTokenBalance = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalance = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                var swapRito = poolLatestBlock.Balances[pool.Token0].ToBalanceDecimal() / poolLatestBlock.Balances[pool.Token1].ToBalanceDecimal();

                // ops, someone swapped
                var w2 = Restore(otherAccountPrivateKey);
                await w2.Sync(client);
                var otherAmount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                var otherResult = await w2.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, otherAmount, swapRito, 0m);
                Assert.IsTrue(otherResult.ResultCode == APIResultCodes.Success, $"Failed to swap other account {LyraGlobal.OFFICIALTICKERCODE}: {otherResult.ResultCode}");

                await Task.Delay(3000);

                // then the slippage is triggered
                var amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                var result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, swapRito, 0m);
                Assert.IsTrue(result.ResultCode == APIResultCodes.PoolSwapRitoChanged, $"Failed to swap {LyraGlobal.OFFICIALTICKERCODE}: {result.ResultCode}");

                amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, swapRito, 0.001m);
                Assert.IsTrue(result.ResultCode == APIResultCodes.SwapSlippageExcceeded, $"Failed to swap {LyraGlobal.OFFICIALTICKERCODE}: {result.ResultCode}");

                amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, swapRito, 0.1m);
                Assert.IsTrue(result.ResultCode == APIResultCodes.Success, $"Failed to swap {LyraGlobal.OFFICIALTICKERCODE}: {result.ResultCode}");

                //await Task.Delay(3000);

                //var amountToGet = Math.Round(amount / swapRito, 8);
                //await w1.Sync(client);

                //var testTokenBalance2 = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                //var lyrBalance2 = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                //Assert.AreEqual(testTokenBalance + amountToGet, testTokenBalance2);
                //Assert.AreEqual(lyrBalance - 1 - amount, lyrBalance2);
            }
            finally
            {
                semaphore.Release();
            }
        }

        [TestMethod]
        public async Task SwapCoinWrong()
        {
            try
            {
                await semaphore.WaitAsync();

                var pool = await client.GetPool(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);
                var poolLatestBlock = pool.GetBlock() as TransactionBlock;

                Assert.IsTrue(poolLatestBlock.Balances[pool.Token0] > 0 && poolLatestBlock.Balances[pool.Token1] > 0, "No liquidate in pool.");

                var w1 = Restore(testPrivateKey);
                await w1.Sync(client);

                var testTokenBalance = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalance = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                var swapRito = Math.Round(poolLatestBlock.Balances[pool.Token0].ToBalanceDecimal() / poolLatestBlock.Balances[pool.Token1].ToBalanceDecimal(), LyraGlobal.RITOPRECISION);

                // send wrong token
                var amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                var result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, testTokenB, amount, swapRito, 0);
                Assert.IsTrue(result.ResultCode != APIResultCodes.Success, $"Should failed but: {result.ResultCode}");

                amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenB, LyraGlobal.OFFICIALTICKERCODE, amount, swapRito, 0m);
                Assert.IsTrue(result.ResultCode != APIResultCodes.Success, $"Should failed but: {result.ResultCode}");

                amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                result = await w1.SwapToken(testTokenB, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, swapRito, 0m);
                Assert.IsTrue(result.ResultCode != APIResultCodes.Success, $"Should failed but: {result.ResultCode}");

                amount = poolLatestBlock.Balances[pool.Token0].ToBalanceDecimal() / 2 + 0.1m;
                result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, swapRito, 0m);
                Assert.IsTrue(result.ResultCode != APIResultCodes.Success, $"Should failed but: {result.ResultCode}");

                amount = poolLatestBlock.Balances[pool.Token0].ToBalanceDecimal() / 2 + 1000m;
                result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, swapRito, 0m);
                Assert.IsTrue(result.ResultCode != APIResultCodes.Success, $"Should failed but: {result.ResultCode}");

                amount = poolLatestBlock.Balances[pool.Token0].ToBalanceDecimal();
                result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, swapRito, 0m);
                Assert.IsTrue(result.ResultCode != APIResultCodes.Success, $"Should failed but: {result.ResultCode}");

                amount = 10000000000m;
                result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, swapRito, 0m);
                Assert.IsTrue(result.ResultCode != APIResultCodes.Success, $"Should failed but: {result.ResultCode}");

                await Task.Delay(3000);
                await w1.Sync(client);
                // make sure the balance is not changed.
                var testTokenBalancex = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalancex = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
                Assert.AreEqual(testTokenBalance, testTokenBalancex);
                Assert.AreEqual(lyrBalance, lyrBalancex);

                // then ok token
                amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, swapRito, 0m);
                Assert.IsTrue(result.ResultCode == APIResultCodes.Success, $"Failed to swap {LyraGlobal.OFFICIALTICKERCODE}: {result.ResultCode}");

                await Task.Delay(3000);

                var amountToGet = Math.Round(amount / swapRito, 8);
                await w1.Sync(client);

                var testTokenBalance2 = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalance2 = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                Assert.AreEqual(testTokenBalance + amountToGet, testTokenBalance2);
                Assert.AreEqual(lyrBalance - 1 - amount, lyrBalance2);

            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
