using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests.JsonRPC
{
    [TestClass]
    public class UT_WalletJsonRpc : JsonRpcTestBase
    {
        // used as test account
        string _privateKey;
        string _accountId;
        LyraRestClient client = LyraRestClient.Create(TestConfig.networkId, "Windows", "UnitTest", "1.0");

        public UT_WalletJsonRpc()
        {
            (_privateKey, _accountId) = Signatures.GenerateWallet();
        }

        [TestMethod]
        public async Task BalanceAsync()
        {
            await TestProcAsync(async (jsonRpc, cancellationToken) =>
            {
                var result = await jsonRpc.InvokeWithCancellationAsync<JObject>("Balance", new object[] { UT_TransitWallet.ADDRESS_ID_1 }, cancellationToken);
                Assert.IsNotNull(result);
                var balance = result["balance"].ToObject<Dictionary<string, decimal>>();
                Assert.IsNotNull(balance);
            }).ConfigureAwait(true);
        }

        [TestMethod]
        public async Task BalanceEmptyAsync()
        {
            var (_, accoutId) = Signatures.GenerateWallet();
            await TestProcAsync(async (jsonRpc, cancellationToken) =>
            {
                var result = await jsonRpc.InvokeWithCancellationAsync<JObject>("Balance", new object[] { accoutId }, cancellationToken);
                Assert.IsNull(result["balance"]);
            }).ConfigureAwait(true);
        }

        [TestMethod]
        public async Task ReceiveTestAsync()
        {
            var memStor = new AccountInMemoryStorage();
            Wallet.Create(memStor, "tmpAcct", "", TestConfig.networkId, UT_TransitWallet.PRIVATE_KEY_1);
            var w1 = Wallet.Open(memStor, "tmpAcct", "");

            var syncResult = await w1.Sync(client);
            Assert.AreEqual(APIResultCodes.Success, syncResult, $"Error Sycn: {syncResult}");

            await TestProcAsync(async (jsonRpc, cancellationToken) =>
            {
                // at first, wallet is empty
                var result = await jsonRpc.InvokeWithCancellationAsync<JObject>("Balance", new object[] { _accountId }, cancellationToken);
                Assert.IsNotNull(result);
                Assert.IsNull(result["balance"]);
                Assert.AreEqual(false, result["unreceived"].Value<bool>());

                // we send 10 LYR to it
                var sendResult = await w1.Send(10, _accountId);
                Assert.IsTrue(sendResult.Successful());

                var result2 = await jsonRpc.InvokeWithCancellationAsync<JObject>("Balance", new object[] { _accountId }, cancellationToken);
                Assert.IsNotNull(result2);
                Assert.IsNull(result2["balance"]);
                Assert.AreEqual(true, result2["unreceived"].Value<bool>());

                // do receive, have 10 LYR
                var result3 = await jsonRpc.InvokeWithCancellationAsync<JObject>("Receive", new object[] { _accountId }, cancellationToken);
                Assert.IsNotNull(result3);
                Assert.IsNotNull(result3["balance"]);
                Assert.AreEqual(false, result3["unreceived"].Value<bool>());

                var balance = result3["balance"].ToObject<Dictionary<string, decimal>>();
                Assert.IsNotNull(balance);
                Assert.AreEqual(10, balance["LYR"]);

                // send 5 back
                var result4 = await jsonRpc.InvokeWithCancellationAsync<JObject>("Send", new object[] { _accountId, 5, UT_TransitWallet.ADDRESS_ID_1, LyraGlobal.OFFICIALTICKERCODE }, cancellationToken);
                Assert.IsNotNull(result4);
                Assert.IsNotNull(result4["balance"]);
                Assert.AreEqual(false, result4["unreceived"].Value<bool>());
                var balance4 = result4["balance"].ToObject<Dictionary<string, decimal>>();
                Assert.IsNotNull(balance4);
                Assert.AreEqual(4, balance["LYR"]);

            }).ConfigureAwait(true);
        }

        protected override string SignMessage(string message)
        {
            return Signatures.GetSignature(_privateKey, message, _accountId);
        }
    }
}
