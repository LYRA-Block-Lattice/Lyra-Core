using DexServer.Ext;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_DexClient
    {
        //[TestMethod]
        public async Task TestGenerateWalletAsync()
        {
            var (pvtx, pubx) = Signatures.GenerateWallet();

            var dc = new DexClient("devnet");
            var r1 = await dc.CreateWalletAsync(pubx, "tron", "mainnet", "", "", "");
            Assert.IsTrue(r1.Success);
            var extw = r1 as DexAddress;
            Assert.IsTrue(extw.Address.StartsWith('T'));
        }
    }
}
