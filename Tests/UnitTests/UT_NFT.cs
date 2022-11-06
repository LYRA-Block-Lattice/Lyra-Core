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

            var id = new Guid();
            var ret = await testWallet.CreateNFTAsync(id.ToString(), "NFT", "a great nft", 10, true, "", "", "", "", null);
            Assert.IsTrue(ret.Successful(), $"Create NFT failed: {ret.ResultMessage}");
        }
    }
}
