using FluentAssertions;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        Random _rand = new Random();

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

                // sync wallet first
                var w1 = Restore(testPrivateKey);
                var syncResult = await w1.SyncAsync(client);
                Assert.IsTrue(syncResult == APIResultCodes.Success);

                var (pvt, pub) = Signatures.GenerateWallet();
                var wx = Restore(pvt);
                await w1.SendAsync(500, wx.AccountId);
                await Task.Delay(1000);
                await wx.SyncAsync(client);
                await Task.Delay(1000);
                int totalStaking = 2;
                // test create profiting account
                var result = await wx.CreateProfitingAccountAsync($"UT{_rand.Next()}", ProfitingType.Node, 1m, totalStaking);
                Assert.IsTrue(result.ResultCode == APIResultCodes.Success, $"Result: {result.ResultCode}");

                var pgen = result.GetBlock() as ProfitingBlock;
                Assert.IsNotNull(pgen);

                Console.WriteLine($"Profit account: {pgen.AccountID}");

                var stopwatch = Stopwatch.StartNew();
                List<Wallet> stkWallets1 = new List<Wallet>();
                List<Wallet> stkWallets2 = new List<Wallet>();
                for (int i = 0; i < totalStaking; i++)
                {
                    var (pvtx, pubx) = Signatures.GenerateWallet();
                    var stkx = Restore(pvtx);
                    stkWallets1.Add(stkx);   

                    await w1.SendAsync(30m, stkx.AccountId);
                    await stkx.SyncAsync(client);

                    // test create staking account
                    var result2 = await stkx.CreateStakingAccountAsync($"UT{_rand.Next()}", pgen.AccountID, 1000, true);
                    Assert.IsTrue(result2.ResultCode == APIResultCodes.Success, $"Result2: {result2.ResultCode}");

                    var stkgen = result2.GetBlock() as StakingBlock;
                    Assert.IsNotNull(stkgen);

                    // test add staking
                    var result3 = await stkx.AddStakingAsync(stkgen.AccountID, 10m);
                    Assert.IsTrue(result3.ResultCode == APIResultCodes.Success, $"Result3: {result3.ResultCode}");
                }
                for (int i = 0; i < totalStaking; i++)
                {
                    var (pvtx, pubx) = Signatures.GenerateWallet();
                    var stkx = Restore(pvtx);
                    stkWallets2.Add(stkx);

                    await w1.SendAsync(30m, stkx.AccountId);
                    await stkx.SyncAsync(client);

                    // test create staking account
                    var result2 = await stkx.CreateStakingAccountAsync($"UT{_rand.Next()}", pgen.AccountID, 1000, false);
                    Assert.IsTrue(result2.ResultCode == APIResultCodes.Success, $"Result2: {result2.ResultCode}");

                    var stkgen = result2.GetBlock() as StakingBlock;
                    Assert.IsNotNull(stkgen);

                    // test add staking
                    var result3 = await stkx.AddStakingAsync(stkgen.AccountID, 10m);
                    Assert.IsTrue(result3.ResultCode == APIResultCodes.Success, $"Result3: {result3.ResultCode}");
                }
                stopwatch.Stop();
                Console.WriteLine($"create staking uses {stopwatch.ElapsedMilliseconds} ms");
                await w1.SendAsync(200, pgen.AccountID);
                await wx.CreateDividendsAsync(pgen.AccountID);
                await Task.Delay(20000);

                foreach (var stkx in stkWallets1)
                {
                    var stkactcall = await stkx.RPC.GetAllBrokerAccountsForOwnerAsync(stkx.AccountId);
                    Assert.IsTrue(stkactcall.Successful());

                    var stkact = stkactcall.GetBlocks().First(a => a is IStaking) as IStaking;
                    var last = await stkx.RPC.GetLastBlockAsync((stkact as TransactionBlock).AccountID);
                    Assert.AreEqual(20m, (last.GetBlock() as TransactionBlock).Balances["LYR"].ToBalanceDecimal());

                    await stkx.SyncAsync(client);
                    Assert.AreEqual(8m, stkx.BaseBalance);
                }

                foreach (var stkx in stkWallets2)
                {
                    var stkactcall = await stkx.RPC.GetAllBrokerAccountsForOwnerAsync(stkx.AccountId);
                    Assert.IsTrue(stkactcall.Successful());

                    var stkact = stkactcall.GetBlocks().First(a => a is IStaking) as IStaking;
                    var last = await stkx.RPC.GetLastBlockAsync((stkact as TransactionBlock).AccountID);
                    Assert.AreEqual(10m, (last.GetBlock() as TransactionBlock).Balances["LYR"].ToBalanceDecimal());

                    await stkx.SyncAsync(client);
                    Assert.AreEqual(18m, stkx.BaseBalance);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }


    }
}
