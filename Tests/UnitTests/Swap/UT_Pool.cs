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

namespace UnitTests.Swap
{
    [TestClass]
    public class UT_Pool
    {
        readonly string networkId = TestConfig.networkId;
        ILyraAPI client;
        private readonly string testTokenA = "unittest/PoolCoinC1";  // change name when chain crupt
        private readonly string testTokenB = "unittest/PoolCoinX";

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
            await w1.SyncAsync(client);

            var balances = w1.GetLatestBlock().Balances;
            Assert.IsTrue(balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() > 100000m, "Insufficient funds: LYR");

            // make sure we have 2 test token
            var genResult = await client.GetTokenGenesisBlockAsync(testPublicKey, testTokenA, await SignAPIAsync());
            if(genResult.ResultCode == APIResultCodes.TokenGenesisBlockNotFound)
            {
                var secs = testTokenA.Split('/');
                var result = await w1.CreateTokenAsync(secs[1], secs[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(result.Successful(), "Failed to create token: " + result.ResultCode);
                await w1.SyncAsync(client);
            }

            genResult = await client.GetTokenGenesisBlockAsync(testPublicKey, testTokenB, await SignAPIAsync());
            if (genResult.ResultCode == APIResultCodes.TokenGenesisBlockNotFound)
            {
                var secs = testTokenB.Split('/');
                var result = await w1.CreateTokenAsync(secs[1], secs[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(result.Successful(), "Failed to create token: " + result.ResultCode);
                await w1.SyncAsync(client);
            }

            var pool = await client.GetPoolAsync(LyraGlobal.OFFICIALTICKERCODE, testTokenA);
            if(pool.PoolAccountId == null)
            {
                var token0 = LyraGlobal.OFFICIALTICKERCODE;
                var token1 = testTokenA;

                var poolCreateResult = await w1.CreateLiquidatePoolAsync(token0, token1);
                await Task.Delay(3000);     // give consens network time to create it.
                Assert.IsTrue(poolCreateResult.ResultCode == APIResultCodes.Success, "Can't create pool for " + token1);
            }

            await w1.SyncAsync(client);
            balances = w1.GetLatestBlock().Balances;
            Assert.IsTrue(balances[testTokenA].ToBalanceDecimal() > 100000m, "Insufficient funds: " + testTokenA);
        }
        
        [TestMethod]
        public async Task TestAll()
        {
            await APoolSetupProperlyAsync();
            await PoolDepositionAsync();
            await SwapCoinWrongAsync();
            await SwapLYRToTokenAAsync();
            await SwapTokenAToLYRAsync();
            await SwapWithSlippageAsync();
            await ZPoolWithdrawAsync();
        }


        public async Task APoolSetupProperlyAsync()
        {
            await Task.Delay(1000);
            var pool = await client.GetPoolAsync(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
            Assert.IsNotNull(pool.PoolAccountId);
            pool.PoolAccountId.Should().StartWith("L");
        }

        public async Task PoolDepositionAsync()
        {
            try
            {
                await semaphore.WaitAsync();

                var pool = await client.GetPoolAsync(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);

                var w1 = Restore(testPrivateKey);
                await w1.SyncAsync(client);

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
                await Task.Delay(3000);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task ZPoolWithdrawAsync()
        {
            try
            {
                await semaphore.WaitAsync();

                var pool = await client.GetPoolAsync(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);
                Assert.IsTrue(LyraGlobal.OFFICIALTICKERCODE == pool.Token0);
                Assert.IsTrue(testTokenA == pool.Token1);

                var w1 = Restore(testPrivateKey);
                await w1.SyncAsync(client);

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

                await Task.Delay(5000);
                pool = await client.GetPoolAsync(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);

                poolLatest = pool.GetBlock() as TransactionBlock;
                Assert.IsNotNull(poolLatest);

                poolWithShare = poolLatest as IPool;
                Assert.IsNotNull(poolWithShare);

                Assert.IsFalse(poolWithShare.Shares.ContainsKey(w1.AccountId), "The pool share is still there.");

                await w1.SyncAsync(client);
                // token0 is lyr, and fee + 1m = 2
                Assert.AreEqual(token0BalanceBefore + token0ShouldReceive - 2, w1.GetLatestBlock().Balances[pool.Token0].ToBalanceDecimal());
                Assert.AreEqual(token1BalanceBefore + token1ShouldReceive, w1.GetLatestBlock().Balances[pool.Token1].ToBalanceDecimal());
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task SwapTokenAToLYRAsync()
        {
            try
            {
                await semaphore.WaitAsync();

                var pool = await client.GetPoolAsync(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);
                var poolLatestBlock = pool.GetBlock() as TransactionBlock;

                Assert.IsTrue(poolLatestBlock.Balances[pool.Token0] > 0 && poolLatestBlock.Balances[pool.Token1] > 0, "No liquidate in pool.");

                var w1 = Restore(testPrivateKey);
                await w1.SyncAsync(client);

                var testTokenBalance = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalance = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                var amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);

                var cal1 = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, testTokenA, poolLatestBlock, testTokenA, amount, 0);

                var result = await w1.SwapTokenAsync(LyraGlobal.OFFICIALTICKERCODE, testTokenA, testTokenA, amount, cal1.MinimumReceived);
                Assert.IsTrue(result.ResultCode == APIResultCodes.Success, $"Failed to swap {testTokenA}: {result.ResultCode}");
                await Task.Delay(5000);

                var poolGenesisResult = await client.GetBlockByIndexAsync(poolLatestBlock.AccountID, 1);
                var block = poolGenesisResult.GetBlock();
                var poolGenesis = block as PoolGenesisBlock;
                Assert.IsTrue(poolGenesisResult.ResultCode == APIResultCodes.Success, $"get gensis returns {poolGenesisResult.ResultCode}");
                Assert.IsNotNull(poolGenesis, "Can't get pool genesis block.");
                var sc = new SwapCalculator(poolGenesis.Token0, poolGenesis.Token1, poolLatestBlock,
                    testTokenA, amount, 0);

                var amountToGet = sc.SwapOutAmount;

                await w1.SyncAsync(client);

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

        public async Task SwapLYRToTokenAAsync()
        {
            try
            {
                await semaphore.WaitAsync();

                var pool = await client.GetPoolAsync(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);
                var poolLatestBlock = pool.GetBlock() as TransactionBlock;

                Assert.IsTrue(poolLatestBlock.Balances[pool.Token0] > 0 && poolLatestBlock.Balances[pool.Token1] > 0, "No liquidate in pool.");

                var w1 = Restore(testPrivateKey);
                await w1.SyncAsync(client);

                var testTokenBalance = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalance = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                var amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);

                var cal1 = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, testTokenA, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, amount, 0);

                var result = await w1.SwapTokenAsync(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, cal1.MinimumReceived);
                Assert.IsTrue(result.ResultCode == APIResultCodes.Success, $"Failed to swap {LyraGlobal.OFFICIALTICKERCODE}: {result.ResultCode}");
                await Task.Delay(9000);

                // then the pool should receive the funds
                var poolInResult = await client.GetBlockByIndexAsync(pool.PoolAccountId, poolLatestBlock.Height + 1);
                Assert.IsTrue(poolInResult.ResultCode == APIResultCodes.Success, "failed to get pool in block. " + poolInResult.ResultCode);
                var poolIn = poolInResult.GetBlock() as PoolSwapInBlock;
                Assert.IsNotNull(poolIn);

                var chgs = poolIn.GetBalanceChanges(poolLatestBlock);
                Assert.AreEqual(1, chgs.Changes.Count);
                Assert.AreEqual(LyraGlobal.OFFICIALTICKERCODE, chgs.Changes.First().Key);
                Assert.AreEqual(amount, chgs.Changes.First().Value);

                var poolGenesisResult = await client.GetBlockByIndexAsync(poolLatestBlock.AccountID, 1);
                var block = poolGenesisResult.GetBlock();
                var poolGenesis = block as PoolGenesisBlock;
                Assert.IsTrue(poolGenesisResult.ResultCode == APIResultCodes.Success, $"get gensis returns {poolGenesisResult.ResultCode}");
                Assert.IsNotNull(poolGenesis, "Can't get pool genesis block.");

                var amountToGet = cal1.SwapOutAmount;

                await w1.SyncAsync(client);

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

        public async Task SwapWithSlippageAsync()
        {
            try
            {
                await semaphore.WaitAsync();

                var pool = await client.GetPoolAsync(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);
                var poolLatestBlock = pool.GetBlock() as TransactionBlock;

                Assert.IsTrue(poolLatestBlock.Balances[pool.Token0] > 0 && poolLatestBlock.Balances[pool.Token1] > 0, "No liquidate in pool.");

                var w1 = Restore(testPrivateKey);
                await w1.SyncAsync(client);

                var testTokenBalance = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalance = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                // ops, someone swapped
                var w2 = Restore(otherAccountPrivateKey);
                var w2result = await w2.SyncAsync(client);
                Assert.IsTrue(w2result == APIResultCodes.Success, $"W2 sync failed: {w2result}");

                var otherAmount = 1m;// Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);

                var cal1 = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, testTokenA, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, otherAmount, 0);

                var otherResult = await w2.SwapTokenAsync(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, otherAmount, cal1.MinimumReceived);
                Assert.IsTrue(otherResult.ResultCode == APIResultCodes.Success, $"Failed to swap other account {LyraGlobal.OFFICIALTICKERCODE}: {otherResult.ResultCode}");

                await Task.Delay(6000);

                // then the slippage is triggered
                var amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                var cal2 = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, testTokenA, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, amount, 0);
                var result = await w1.SwapTokenAsync(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, cal2.SwapOutAmount);
                Assert.IsTrue(result.ResultCode == APIResultCodes.SwapSlippageExcceeded, $"Should Failed to swap {LyraGlobal.OFFICIALTICKERCODE}: {result.ResultCode}");

                amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                var cal3 = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, testTokenA, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, amount, 0.001m);
                result = await w1.SwapTokenAsync(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, cal3.SwapOutAmount + 1000);
                Assert.IsTrue(result.ResultCode == APIResultCodes.SwapSlippageExcceeded, $"Should Failed to swap {LyraGlobal.OFFICIALTICKERCODE}: {result.ResultCode}");

                amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                var cal4 = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, testTokenA, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, amount, 0.1m);
                result = await w1.SwapTokenAsync(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, cal4.MinimumReceived);
                Assert.IsTrue(result.ResultCode == APIResultCodes.Success, $"Failed to swap {LyraGlobal.OFFICIALTICKERCODE}: {result.ResultCode}");

                await Task.Delay(6000);

                var amountToGet = cal4.MinimumReceived;
                await w1.SyncAsync(client);

                var testTokenBalance2 = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalance2 = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                Assert.IsTrue(testTokenBalance + amountToGet <= testTokenBalance2, 
                    $"testTokenBalance + amountToGet is {testTokenBalance + amountToGet}, testTokenBalance2 is {testTokenBalance2}");
                Assert.AreEqual(lyrBalance - 1 - amount, lyrBalance2);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task SwapCoinWrongAsync()
        {
            try
            {
                await semaphore.WaitAsync();

                var pool = await client.GetPoolAsync(testTokenA, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNotNull(pool.PoolAccountId);
                var poolLatestBlock = pool.GetBlock() as TransactionBlock;

                Assert.IsTrue(poolLatestBlock.Balances[pool.Token0] > 0 && poolLatestBlock.Balances[pool.Token1] > 0, "No liquidate in pool.");

                var w1 = Restore(testPrivateKey);
                await w1.SyncAsync(client);

                var testTokenBalance = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalance = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                // send wrong token
                var amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                var cal = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, testTokenA, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, amount, 0);

                var result = await w1.SwapTokenAsync(LyraGlobal.OFFICIALTICKERCODE, testTokenA, testTokenB, amount, cal.SwapOutAmount);
                Assert.IsTrue(result.ResultCode != APIResultCodes.Success, $"Should failed but: {result.ResultCode}");

                amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                cal = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, testTokenA, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, amount, 0);
                result = await w1.SwapTokenAsync(LyraGlobal.OFFICIALTICKERCODE, testTokenB, LyraGlobal.OFFICIALTICKERCODE, amount, cal.SwapOutAmount);
                Assert.IsTrue(result.ResultCode != APIResultCodes.Success, $"Should failed but: {result.ResultCode}");

                amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                result = await w1.SwapTokenAsync(testTokenB, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, cal.SwapOutAmount);
                Assert.IsTrue(result.ResultCode != APIResultCodes.Success, $"Should failed but: {result.ResultCode}");

                amount = lyrBalance + 1;
                result = await w1.SwapTokenAsync(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, cal.SwapOutAmount);
                Assert.IsTrue(result.ResultCode != APIResultCodes.Success, $"Should failed but: {result.ResultCode}");

                //amount = poolLatestBlock.Balances[pool.Token0].ToBalanceDecimal() / 2 + 1000m;
                //result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, cal.SwapOutAmount);
                //Assert.IsTrue(result.ResultCode != APIResultCodes.Success, $"Should failed but: {result.ResultCode}");

                //amount = poolLatestBlock.Balances[pool.Token0].ToBalanceDecimal();
                //result = await w1.SwapToken(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, cal.SwapOutAmount);
                //Assert.IsTrue(result.ResultCode != APIResultCodes.Success, $"Should failed but: {result.ResultCode}");

                amount = 1000000000000m;
                try
                {
                    result = await w1.SwapTokenAsync(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, cal.SwapOutAmount);
                    Assert.Fail("Should has over flow exception.");
                }
                catch { }

                await Task.Delay(3000);
                await w1.SyncAsync(client);
                // make sure the balance is not changed.
                var testTokenBalancex = w1.GetLatestBlock().Balances[testTokenA].ToBalanceDecimal();
                var lyrBalancex = w1.GetLatestBlock().Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
                Assert.AreEqual(testTokenBalance, testTokenBalancex);
                Assert.AreEqual(lyrBalance, lyrBalancex);

                // then ok token
                amount = Math.Round((decimal)((new Random().NextDouble() + 0.07) * 1000), 8);
                cal = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, testTokenA, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, amount, 0);
                result = await w1.SwapTokenAsync(LyraGlobal.OFFICIALTICKERCODE, testTokenA, LyraGlobal.OFFICIALTICKERCODE, amount, cal.MinimumReceived);
                Assert.IsTrue(result.ResultCode == APIResultCodes.Success, $"Failed to swap {LyraGlobal.OFFICIALTICKERCODE}: {result.ResultCode}");

                await Task.Delay(15000);

                var poolGenesisResult = await client.GetBlockByIndexAsync(poolLatestBlock.AccountID, 1);
                var block = poolGenesisResult.GetBlock();
                var poolGenesis = block as PoolGenesisBlock;
                Assert.IsTrue(poolGenesisResult.ResultCode == APIResultCodes.Success, $"get gensis returns {poolGenesisResult.ResultCode}");
                Assert.IsNotNull(poolGenesis, "Can't get pool genesis block.");
                var sc = new SwapCalculator(poolGenesis.Token0, poolGenesis.Token1, poolLatestBlock,
                    LyraGlobal.OFFICIALTICKERCODE, amount, 0);
                var amountToGet = sc.SwapOutAmount;

                await w1.SyncAsync(client);

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
