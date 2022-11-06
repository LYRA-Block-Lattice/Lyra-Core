using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
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
        public async Task NFT_Tests()
        {
            await SetupWallets(TestConfig.networkId);

            var metauri = "https://lyra.live/meta/some";
            var rand = new Random();

            var name = $"a great nft ({rand.NextInt64()})";
            var ret = await testWallet.CreateNFTAsync(name, "a nft for unit test", 10, metauri);
            Assert.IsTrue(ret.Successful(), $"Create NFT failed: {ret.ResultMessage}");

            // send
            var nftgen = testWallet.GetLastSyncBlock() as TokenGenesisBlock;
            Assert.IsNotNull(nftgen);
            Assert.AreEqual(name, nftgen.Custom1);

            var tickrToSend = nftgen.Ticker + "#0";
            var findSendRet = await testWallet.RPC.FindNFTGenesisSendAsync(testPublicKey, nftgen.Ticker, "0");
            Assert.AreEqual(APIResultCodes.BlockNotFound, findSendRet.ResultCode);

            var nft = testWallet.IssueNFT(nftgen.Ticker, "0");
            var sendRet = await testWallet.SendAsync(1m, test2PublicKey, nftgen.Ticker, nft);
            Assert.IsTrue(sendRet.Successful(), $"Faid to send NFT: {sendRet.ResultCode}");
        }
    }
}
