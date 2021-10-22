using FluentAssertions;
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
using System.Net;

namespace UnitTests
{
    /// <summary>
    /// Note: send at least 1M LYR to account 1. create a token named "unittest/trans" in account 1.
    /// </summary>
    [TestClass]
    public class UT_LightWallet
    {
        public const string PRIVATE_KEY_1 = "dkrwRdqNjEEshpLuEPPqc6zM1HM3nzGjsYts39zzA1iUypcpj";
        const string PRIVATE_KEY_2 = "Hc3XcZgZ1d2jRxhNojN1gnKHv5SBs15mR8K2SdkBbycrgAjPr";

        public const string ADDRESS_ID_1 = "LUTG2E1mdpGk5Qtq9BUgwZDWhUeZc14Xfw2pAvAdKoacvgRBU3atwtrQeoY3evm5C7TXRz3Q5nwPEUHj9p7CBDE6kQTQMy";
        const string ADDRESS_ID_2 = "LUTAq9MFf4vaqbEEDHsRj8SUbLWoKptndaUqXSnYbi7mC1cXajts6fWXhQUuwR4ZX7DnvERkUMpwXKf4XKk4NjVMxqYvmn";

        //private string testToken = "unittest/trans";

        private SemaphoreSlim semaphore = new SemaphoreSlim(1);      // we need to run tests in serial

        public static LightWallet Restore(string privateKey)
        {
            return new LightWallet(new NetworkCredential("", privateKey).SecurePassword, TestConfig.networkId);
        }

        [TestInitialize]
        public async Task UT_LightWallet_SetupAsync()
        {
            var w1 = Restore(PRIVATE_KEY_1);
            await w1.ReceiveAsync();
            var balances = await w1.GetBalanceAsync();
            Assert.IsTrue(balances.balance[LyraGlobal.OFFICIALTICKERCODE] > 50000m, "Insufficient funds: LYR");
        }

        [TestMethod]
        public void RestoreTest1()
        {
            var wallet1 = Restore(PRIVATE_KEY_1);
            Assert.AreEqual(ADDRESS_ID_1, wallet1.AccountId);
        }

        [TestMethod]
        public async Task SendReceiveTestAsync()
        {
            try
            {
                await semaphore.WaitAsync();

                var w1 = Restore(PRIVATE_KEY_1);
                var s1 = await w1.ReceiveAsync();
                Assert.IsTrue(s1.balance != null, $"Failed to receive token: {s1}");

                var w2 = Restore(PRIVATE_KEY_2);
                var s2 = await w2.ReceiveAsync();
                Assert.IsTrue(s2.balance != null, $"Failed to receive token: {s2}");

                var b1 = s1.balance;

                var b1Before = b1[LyraGlobal.OFFICIALTICKERCODE];
                Assert.IsTrue(b1Before > 100000m && b1Before < 30000000m);

                var b2Balances = s2.balance;
                var b2Before = b2Balances?.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) == true ? b2Balances[LyraGlobal.OFFICIALTICKERCODE] : 0m;

                var amount = Math.Round((decimal)((new Random().NextDouble() + 0.03) * 1000), 8);
                //var amounts = new Dictionary<string, decimal>();
                //amounts.Add(LyraGlobal.OFFICIALTICKERCODE, amount);
                var sendResult = await w1.SendAsync(amount, w2.AccountId, LyraGlobal.OFFICIALTICKERCODE);
                await w2.ReceiveAsync();

                var b1After = (await w1.GetBalanceAsync()).balance[LyraGlobal.OFFICIALTICKERCODE];
                var b2After = (await w2.GetBalanceAsync()).balance[LyraGlobal.OFFICIALTICKERCODE];

                // the shit deciml deserialize bug. https://stackoverflow.com/questions/24051206/handling-decimal-values-in-newtonsoft-json
                Assert.AreEqual(Math.Round(b1Before - 1m - amount, 4), Math.Round(b1After, 4));
                Assert.AreEqual(Math.Round(b2Before + amount, 4), Math.Round(b2After, 4));
            }
            finally
            {
                semaphore.Release();
            }
        }

        //[TestMethod]
        //public async System.Threading.Tasks.Task SendMultiTokenTestAsync()
        //{
        //    try
        //    {
        //        await semaphore.WaitAsync();

        //        var w1 = Restore(PRIVATE_KEY_1);
        //        var w2 = Restore(PRIVATE_KEY_2);

        //        var syncResult = await w1.ReceiveAsync();
        //        Assert.AreEqual(syncResult, APIResultCodes.Success);
        //        var b1 = await w1.GetBalanceAsync();

        //        var b1Before = b1[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
        //        var b1Before2 = b1[testToken].ToBalanceDecimal();

        //        Assert.IsTrue(b1Before > 100000m && b1Before < 10000000m);

        //        Assert.IsTrue(APIResultCodes.Success == await w2.ReceiveAsync());
        //        var b2Balance = await w2.GetBalanceAsync();
        //        var b2Before = b2Balance?.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) == true ? b2Balance[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() : 0;
        //        var b2Before2 = b2Balance?.ContainsKey(testToken) == true ? b2Balance[testToken].ToBalanceDecimal() : 0m;

        //        var amount = Math.Round((decimal)((new Random().NextDouble() + 0.03) * 1000), 8);
        //        var amount2 = Math.Round((decimal)((new Random().NextDouble() + 0.03) * 1000), 8);

        //        var amounts = new Dictionary<string, decimal>();
        //        amounts.Add(LyraGlobal.OFFICIALTICKERCODE, amount);
        //        amounts.Add(testToken, amount2);

        //        var sendResult = await w1.SendAsync(amounts, w2.AccountId);
        //        Assert.IsTrue(sendResult == APIResultCodes.Success, "Failed to send token.");

        //        Assert.IsTrue(APIResultCodes.Success == await w2.ReceiveAsync());

        //        var b1After = (await w1.GetBalanceAsync())[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
        //        var b2After = (await w2.GetBalanceAsync())[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
        //        var b1After2 = (await w1.GetBalanceAsync())[testToken].ToBalanceDecimal();
        //        var b2After2 = (await w2.GetBalanceAsync())[testToken].ToBalanceDecimal();

        //        Assert.AreEqual(b1Before - 1m - amount, b1After);
        //        Assert.AreEqual(b2Before + amount, b2After);

        //        Assert.AreEqual(b1Before2 - amount2, b1After2);
        //        Assert.AreEqual(b2Before2 + amount2, b2After2);
        //    }
        //    finally
        //    {
        //        semaphore.Release();
        //    }
        //}

        //[TestMethod]
        //public async Task BurningToken()
        //{
        //    await semaphore.WaitAsync();
        //    var w1 = Restore(PRIVATE_KEY_1);
        //    var syncResult = await w1.ReceiveAsync();
        //    Assert.AreEqual(syncResult, APIResultCodes.Success);
        //    var b1 = await w1.GetBalanceAsync();
        //    var b1Before = b1[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

        //    var amount = Math.Round((decimal)((new Random().NextDouble() + 0.03) * 1000), 8);
        //    var amounts = new Dictionary<string, decimal>();
        //    amounts.Add(LyraGlobal.OFFICIALTICKERCODE, amount);
        //    var sendResult = await w1.SendAsync(amounts, LyraGlobal.BURNINGACCOUNTID);
        //    Assert.IsTrue(sendResult == APIResultCodes.Success, "Failed to send token.");

        //    var b1After = (await w1.GetBalanceAsync())[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();

        //    Assert.AreEqual(b1Before - 1m - amount, b1After);
        //}
    }
}
