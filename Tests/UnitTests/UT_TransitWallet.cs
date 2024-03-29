﻿using FluentAssertions;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Data.Crypto;
using Lyra.Core.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Lyra.Data.Utils;
using Lyra.Data.API;
using Lyra.Core.Blocks;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTests
{
    /// <summary>
    /// Note: send at least 1M LYR to account 1. create a token named "unittest/trans" in account 1.
    /// </summary>
    [TestClass]
    public class UT_TransitWallet
    {
        public const string PRIVATE_KEY_1 = "dkrwRdqNjEEshpLuEPPqc6zM1HM3nzGjsYts39zzA1iUypcpj";
        const string PRIVATE_KEY_2 = "Hc3XcZgZ1d2jRxhNojN1gnKHv5SBs15mR8K2SdkBbycrgAjPr";

        public const string ADDRESS_ID_1 = "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy";
        const string ADDRESS_ID_2 = "LUTAq9MFf4vaqbEEDHsRj8SUbLWoKptndaUqXSnYbi7mC1cXajts6fWXhQUuwR4ZX7DnvERkUMpwXKf4XKk4NjVMxqYvmn";

        private string testToken = "unittest/trans";

        LyraRestClient client = LyraRestClient.Create(TestConfig.networkId, "Windows", "UnitTest", "1.0");

        private SemaphoreSlim semaphore = new SemaphoreSlim(1);      // we need to run tests in serial

        public static TransitWallet Restore(string privateKey)
        {
            var accountId = Signatures.GetAccountIdFromPrivateKey(privateKey);
            return new TransitWallet(accountId, privateKey, LyraRestClient.Create(TestConfig.networkId, "Windows", "UnitTest", "1.0"));
        }

        private async Task<string> SignAPIAsync()
        {
            var lsb = await client.GetLastServiceBlockAsync();
            return Signatures.GetSignature(PRIVATE_KEY_1, lsb.GetBlock().Hash, ADDRESS_ID_1);
        }

        [TestInitialize]
        public async Task UT_TransitWallet_SetupAsync()
        {
            var memStor = new AccountInMemoryStorage();
            Wallet.Create(memStor, "tmpAcct", "", TestConfig.networkId, PRIVATE_KEY_1);
            var w1 = Wallet.Open(memStor, "tmpAcct", "");

            var syncResult = await w1.SyncAsync(client);
            Assert.AreEqual(APIResultCodes.Success, syncResult, $"Error Sycn: {syncResult}");

            var balances = w1.GetLastSyncBlock().Balances;
            Assert.IsTrue(balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() > 50000m, "Insufficient funds: LYR");

            // make sure we have 2 test token
            var genResult = await client.GetTokenGenesisBlockAsync(ADDRESS_ID_1, testToken, await SignAPIAsync());
            if (genResult.ResultCode == APIResultCodes.TokenGenesisBlockNotFound)
            {
                var secs = testToken.Split('/');
                var result = await w1.CreateTokenAsync(secs[1], secs[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(result.Successful(), "Failed to create token: " + result.ResultCode);
                await w1.SyncAsync(client);
            }
        }

        [TestMethod]
        public void RestoreTest1()
        {
            var wallet1 = Restore(PRIVATE_KEY_1);
            Assert.AreEqual(ADDRESS_ID_1, wallet1.AccountId);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task SendTestAsync()
        {
            try
            {
                await semaphore.WaitAsync();

                var w1 = Restore(PRIVATE_KEY_1);
                var s1 = await w1.ReceiveAsync();
                Assert.IsTrue(s1 == APIResultCodes.Success, $"Failed to receive token: {s1}");

                var w2 = Restore(PRIVATE_KEY_2);
                var s2 = await w2.ReceiveAsync();
                Assert.IsTrue(s2 == APIResultCodes.Success, $"Failed to receive token: {s2}");

                var syncResult = await w1.ReceiveAsync();
                Assert.AreEqual(syncResult, APIResultCodes.Success);
                var b1 = await w1.GetBalanceAsync();

                var b1Before = b1[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
                Assert.IsTrue(b1Before > 100000m && b1Before < 50000000m);

                Assert.IsTrue(APIResultCodes.Success == await w2.ReceiveAsync());
                var b2Balances = await w2.GetBalanceAsync();
                var b2Before = b2Balances?.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) == true ? b2Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() : 0m;

                var amount = Math.Round((decimal)((new Random().NextDouble() + 0.03) * 1000), 8);
                var amounts = new Dictionary<string, decimal>();
                amounts.Add(LyraGlobal.OFFICIALTICKERCODE, amount);
                var sendResult = await w1.SendAsync(amounts, w2.AccountId);
                Assert.IsTrue(sendResult == APIResultCodes.Success, $"Failed to send token: {sendResult}");

                Assert.IsTrue(APIResultCodes.Success == await w2.ReceiveAsync());

                var b1After = (await w1.GetBalanceAsync())[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
                var b2After = (await w2.GetBalanceAsync())[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

                Assert.AreEqual(b1Before - 1m - amount, b1After);
                Assert.AreEqual(b2Before + amount, b2After);
            }
            finally
            {
                semaphore.Release();
            }
        }

        [TestMethod]
        public async System.Threading.Tasks.Task SendMultiTokenTestAsync()
        {
            try
            {
                await semaphore.WaitAsync();

                var w1 = Restore(PRIVATE_KEY_1);
                var w2 = Restore(PRIVATE_KEY_2);

                var syncResult = await w1.ReceiveAsync();
                Assert.AreEqual(syncResult, APIResultCodes.Success);
                var b1 = await w1.GetBalanceAsync();

                var b1Before = b1[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
                var b1Before2 = b1[testToken].ToBalanceDecimal();

                Assert.IsTrue(b1Before > 100000m && b1Before < 50000000m);

                Assert.IsTrue(APIResultCodes.Success == await w2.ReceiveAsync());
                var b2Balance = await w2.GetBalanceAsync();
                var b2Before = b2Balance?.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) == true ? b2Balance[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() : 0;
                var b2Before2 = b2Balance?.ContainsKey(testToken) == true ? b2Balance[testToken].ToBalanceDecimal() : 0m;

                var amount = Math.Round((decimal)((new Random().NextDouble() + 0.03) * 1000), 8);
                var amount2 = Math.Round((decimal)((new Random().NextDouble() + 0.03) * 1000), 8);

                var amounts = new Dictionary<string, decimal>();
                amounts.Add(LyraGlobal.OFFICIALTICKERCODE, amount);
                amounts.Add(testToken, amount2);

                var sendResult = await w1.SendAsync(amounts, w2.AccountId);
                Assert.IsTrue(sendResult == APIResultCodes.Success, "Failed to send token.");

                Assert.IsTrue(APIResultCodes.Success == await w2.ReceiveAsync());

                var b1After = (await w1.GetBalanceAsync())[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
                var b2After = (await w2.GetBalanceAsync())[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
                var b1After2 = (await w1.GetBalanceAsync())[testToken].ToBalanceDecimal();
                var b2After2 = (await w2.GetBalanceAsync())[testToken].ToBalanceDecimal();

                Assert.AreEqual(b1Before - 1m - amount, b1After);
                Assert.AreEqual(b2Before + amount, b2After);

                Assert.AreEqual(b1Before2 - amount2, b1After2);
                Assert.AreEqual(b2Before2 + amount2, b2After2);
            }
            finally
            {
                semaphore.Release();
            }
        }

        [TestMethod]
        public async Task BurningTokenAsync()
        {
            await semaphore.WaitAsync();
            var w1 = Restore(PRIVATE_KEY_1);
            var syncResult = await w1.ReceiveAsync();
            Assert.AreEqual(syncResult, APIResultCodes.Success);
            var b1 = await w1.GetBalanceAsync();
            var b1Before = b1[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

            var amount = Math.Round((decimal)((new Random().NextDouble() + 0.03) * 1000), 8);
            var amounts = new Dictionary<string, decimal>();
            amounts.Add(LyraGlobal.OFFICIALTICKERCODE, amount);
            var sendResult = await w1.SendAsync(amounts, LyraGlobal.BURNINGACCOUNTID);
            Assert.IsTrue(sendResult == APIResultCodes.Success, $"Failed to send token: {sendResult}");

            var b1After = (await w1.GetBalanceAsync())[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

            Assert.AreEqual(b1Before - 1m - amount, b1After);
        }
    }
}
