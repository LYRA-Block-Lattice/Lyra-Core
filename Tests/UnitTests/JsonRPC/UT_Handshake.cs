using Lyra.Core.API;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTests.JsonRPC
{
    [TestClass]
    public class UT_Handshake : JsonRpcClientBase
    {
        public UT_Handshake()
        {
            NetworkId = TestConfig.networkId;
        }
        [TestMethod]
        public async Task VersionAsync()
        {
            await TestProcAsync(async (jsonRpc, cancellationToken) =>
            {
                var result = await jsonRpc.InvokeWithCancellationAsync<JObject>("Status", new object[] { LyraGlobal.NODE_VERSION, TestConfig.networkId }, cancellationToken);
                Assert.IsNotNull(result);
                Assert.AreEqual(true, result["synced"].Value<bool>());
                Assert.AreEqual(TestConfig.networkId, result["networkid"].Value<string>());
            }).ConfigureAwait(true);
        }

        [TestMethod]
        [ExpectedException(typeof(RemoteInvocationException),
            "Client version too low. Need upgrade.")]
        public async Task VersionOutdatedAsync()
        {
            await TestProcAsync(async (jsonRpc, cancellationToken) =>
            {
                var result = await jsonRpc.InvokeWithCancellationAsync<JObject>("Status", new object[] { "1.0.0.0", TestConfig.networkId }, cancellationToken);
                Assert.Fail();
            }).ConfigureAwait(true);
        }

        protected override Task<string> SignMessageAsync(string message)
        {
            throw new NotImplementedException();
        }
    }
}
