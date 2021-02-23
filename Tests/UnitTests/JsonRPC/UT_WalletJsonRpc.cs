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
        [ExpectedException(typeof(RemoteInvocationException),
            "Can't get latest block for account.")]
        public async Task BalanceEmptyAsync()
        {
            var (_, accoutId) = Signatures.GenerateWallet();
            await TestProcAsync(async (jsonRpc, cancellationToken) =>
            {
                var result = await jsonRpc.InvokeWithCancellationAsync<JObject>("Balance", new object[] { accoutId }, cancellationToken);
                Assert.Fail();
            }).ConfigureAwait(true);
        }

    }
}
