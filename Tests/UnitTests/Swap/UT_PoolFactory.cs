using Lyra.Core.API;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests.Swap
{
    [TestClass]
    public class UT_PoolFactory
    {
        [TestMethod]
        public async Task GetPoolFactoryAsync()
        {
            var client = LyraRestClient.Create("devnet", "Windows", "UnitTest", "1.0");
            var pool = await client.GetPool("test1", "test2");
            Assert.IsNull(pool.PoolAccountId);
            Assert.IsTrue(!string.IsNullOrEmpty(pool.PoolFactoryAccountId), "factory not created");
        }

        [TestMethod]
        public async Task CreatePoolAsync()
        {
            var client = LyraRestClient.Create("devnet", "Windows", "UnitTest", "1.0");
            var pool = await client.GetPool("test1", "test2");
            Assert.IsNull(pool.PoolAccountId);
            Assert.IsTrue(!string.IsNullOrEmpty(pool.PoolFactoryAccountId), "factory not created");
        }
    }
}
