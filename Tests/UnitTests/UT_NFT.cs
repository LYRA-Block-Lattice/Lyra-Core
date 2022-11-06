using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_NFT : XTestBase
    {
        [TestMethod]
        public async Task NFT_Genesis()
        {
            await SetupWallets(TestConfig.networkId);

            var metauri = "https://lyra.live/meta/some";
            var ret = await testWallet.CreateNFTAsync("a great nft", "a nft for unit test", 10, metauri);
            Assert.IsTrue(ret.Successful(), $"Create NFT failed: {ret.ResultMessage}");
        }
    }
}
