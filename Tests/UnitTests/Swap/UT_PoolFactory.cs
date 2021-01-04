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
                var secs = testTokenA.Split('/');
                var result = await w1.CreateToken(secs[1], secs[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
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
            try
            {
                await semaphore.WaitAsync();

                var secs = testTokenA.Split('/');
                var tokenName = $"{secs[1]}-{DateTime.UtcNow.Ticks}";
                var tokenFullName = $"{secs[0]}/{tokenName}";

                // create token first
                var w1 = Restore(testPrivateKey);
                await w1.Sync(client);
                var result = await w1.CreateToken(tokenName, secs[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(result.Successful(), $"Failed to create token {tokenFullName}: {result.ResultCode}");

                var pool = await client.GetPool(tokenFullName, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNull(pool.PoolAccountId, "Pool is already exists.");
                Assert.IsNotNull(pool.PoolFactoryAccountId, "Pool Factory should be exists.");

                var token0 = LyraGlobal.OFFICIALTICKERCODE;
                var token1 = tokenFullName;

                var poolCreateResult = await w1.CreateLiquidatePoolAsync(token0, token1);
                Assert.IsTrue(poolCreateResult.ResultCode == APIResultCodes.Success, "Can't create pool for " + token1);
            }
            finally
            {
                semaphore.Release();
            }
        }

        [TestMethod]
        public async Task CreatePoolWrongAsync()
        {
            try
            {
                await semaphore.WaitAsync();

                var secs = testTokenA.Split('/');
                var tokenName = $"{secs[1]}-{DateTime.UtcNow.Ticks}";
                var tokenFullName = $"{secs[0]}/{tokenName}";

                // create token first
                var w1 = Restore(testPrivateKey);
                await w1.Sync(client);
                var result = await w1.CreateToken(tokenName, secs[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(result.Successful(), $"Failed to create token {tokenFullName}: {result.ResultCode}");

                var pool = await client.GetPool(tokenFullName, LyraGlobal.OFFICIALTICKERCODE);
                Assert.IsNull(pool.PoolAccountId, "Pool is already exists.");
                Assert.IsNotNull(pool.PoolFactoryAccountId, "Pool Factory should be exists.");

                var token0 = LyraGlobal.OFFICIALTICKERCODE;
                var token1 = tokenFullName;

                // should fail
                var tags = new Dictionary<string, string>();
                tags.Add("token0", token0);
                tags.Add("token1", "");
                tags.Add(Block.REQSERVICETAG, "");
                var amounts = new Dictionary<string, decimal>();
                amounts.Add(LyraGlobal.OFFICIALTICKERCODE, PoolFactoryBlock.PoolCreateFee);
                var poolCreateResult = await w1.SendEx(pool.PoolFactoryAccountId, amounts, tags);
                Assert.IsTrue(poolCreateResult.ResultCode != APIResultCodes.Success, "Should failed");

                tags = new Dictionary<string, string>();
                tags.Add("token0", token0);
                tags.Add(Block.REQSERVICETAG, "");
                amounts = new Dictionary<string, decimal>();
                amounts.Add(LyraGlobal.OFFICIALTICKERCODE, PoolFactoryBlock.PoolCreateFee);
                poolCreateResult = await w1.SendEx(pool.PoolFactoryAccountId, amounts, tags);
                Assert.IsTrue(poolCreateResult.ResultCode != APIResultCodes.Success, "Should failed");

                tags = new Dictionary<string, string>();
                tags.Add("token0", token0);
                tags.Add("token1", token1);
                amounts = new Dictionary<string, decimal>();
                amounts.Add(LyraGlobal.OFFICIALTICKERCODE, PoolFactoryBlock.PoolCreateFee);
                poolCreateResult = await w1.SendEx(pool.PoolFactoryAccountId, amounts, tags);
                Assert.IsTrue(poolCreateResult.ResultCode != APIResultCodes.Success, "Should failed");

                tags = new Dictionary<string, string>();
                tags.Add("token0", token0);
                tags.Add("token1", token1);
                tags.Add(Block.REQSERVICETAG, "");
                amounts = new Dictionary<string, decimal>();
                amounts.Add(LyraGlobal.OFFICIALTICKERCODE, 1m);
                poolCreateResult = await w1.SendEx(pool.PoolFactoryAccountId, amounts, tags);
                Assert.IsTrue(poolCreateResult.ResultCode != APIResultCodes.Success, "Should failed");

                tags = new Dictionary<string, string>();
                tags.Add("token0", token0);
                tags.Add("token1", token1);
                tags.Add(Block.REQSERVICETAG, "");
                amounts = new Dictionary<string, decimal>();
                amounts.Add(LyraGlobal.OFFICIALTICKERCODE, 10010);
                poolCreateResult = await w1.SendEx(pool.PoolFactoryAccountId, amounts, tags);
                Assert.IsTrue(poolCreateResult.ResultCode != APIResultCodes.Success, "Should failed");

                tags = new Dictionary<string, string>();
                tags.Add("token0", token0);
                tags.Add("token1", token1);
                tags.Add(Block.REQSERVICETAG, "");
                amounts = new Dictionary<string, decimal>();
                amounts.Add(testTokenA, PoolFactoryBlock.PoolCreateFee);
                poolCreateResult = await w1.SendEx(pool.PoolFactoryAccountId, amounts, tags);
                Assert.IsTrue(poolCreateResult.ResultCode != APIResultCodes.Success, "Should failed");

                tags = new Dictionary<string, string>();
                tags.Add("token0", token0);
                tags.Add("token1", token1);
                tags.Add(Block.REQSERVICETAG, "");
                amounts = new Dictionary<string, decimal>();
                amounts.Add(LyraGlobal.OFFICIALTICKERCODE, PoolFactoryBlock.PoolCreateFee);
                poolCreateResult = await w1.SendEx(pool.PoolFactoryAccountId + "a", amounts, tags);
                Assert.IsTrue(poolCreateResult.ResultCode != APIResultCodes.Success, "Should failed");

                tags = new Dictionary<string, string>();
                tags.Add("token0", token0);
                tags.Add("token1", token1);
                tags.Add(Block.REQSERVICETAG, "");
                amounts = new Dictionary<string, decimal>();
                amounts.Add(LyraGlobal.OFFICIALTICKERCODE, PoolFactoryBlock.PoolCreateFee);
                amounts.Add(testTokenA, PoolFactoryBlock.PoolCreateFee);
                poolCreateResult = await w1.SendEx(pool.PoolFactoryAccountId, amounts, tags);
                Assert.IsTrue(poolCreateResult.ResultCode != APIResultCodes.Success, "Should failed");
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
