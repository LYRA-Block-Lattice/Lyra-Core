using Lyra.Core.Accounts;
using Lyra.Core.API;
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
            // xtest for a dynamic chain
            await SetupWallets("devnet");

            await BurnAllNFT();
            return;

            var metauri = "https://lyra.live/meta/some";
            var rand = new Random();

            var name = $"a great nft ({rand.NextInt64()})";
            var ret = await testWallet.CreateNFTAsync(name, "a nft for unit test", 10, metauri);
            Assert.IsTrue(ret.Successful(), $"Create NFT failed: {ret.ResultMessage}");

            // send
            var nftgen = testWallet.GetLastSyncBlock() as TokenGenesisBlock;
            Assert.IsNotNull(nftgen);
            Assert.AreEqual(name, nftgen.Custom1);

            var tickrToSend = nftgen.Ticker;
            var findSendRet = await testWallet.RPC.FindNFTGenesisSendAsync(testPublicKey, nftgen.Ticker, "0");
            Assert.AreEqual(APIResultCodes.BlockNotFound, findSendRet.ResultCode);

            var nft = testWallet.IssueNFT(nftgen.Ticker, null);
            var amounts = new Dictionary<string, decimal>
            {
                { nftgen.Ticker, 1m }
            };
            var sendRet = await testWallet.SendExAsync(test2PublicKey, amounts, null, nft);
            Assert.IsTrue(sendRet.Successful(), $"Faid to send NFT: {sendRet.ResultCode}");
            var sendBlock = testWallet.GetLastSyncBlock();

            // then test2 will receive it.
            await test2Wallet.SyncAsync();
            var recvBlockx = test2Wallet.GetLastSyncBlock();
            Assert.IsTrue(recvBlockx is ReceiveTransferBlock, "not a receive block");
            var recvBlock = recvBlockx as ReceiveTransferBlock;
            Assert.IsTrue(recvBlock.SourceHash == sendBlock.Hash, "not receive properly");
            Assert.IsTrue(recvBlock.Balances.ContainsKey(tickrToSend));
            Assert.IsTrue(recvBlock.Balances[tickrToSend] == 1m.ToBalanceLong());

            // then test2 will send to test3
            var send2ret = await test2Wallet.SendAsync(1m, test3PublicKey, nftgen.Ticker);
            Assert.IsTrue(send2ret.Successful(), $"Faid to send NFT to test3: {send2ret.ResultCode}");

            await test3Wallet.SyncAsync();
            var recvBlockx2 = test3Wallet.GetLastSyncBlock();
            Assert.IsTrue(recvBlockx2 is ReceiveTransferBlock, "not a receive block");
            var recvBlock2 = recvBlockx2 as ReceiveTransferBlock;
            Assert.IsTrue(recvBlock2.SourceHash == send2ret.TxHash, "not receive properly");
            Assert.IsTrue(recvBlock2.Balances.ContainsKey(tickrToSend));
            Assert.IsTrue(recvBlock2.Balances[tickrToSend] == 1m.ToBalanceLong());

            //await BurnAllNFT();
            //name = $"a great nft ({rand.NextInt64()})";
            //ret = await testWallet.CreateNFTAsync(name, "a nft for unit test", 10, metauri);
            //Assert.IsTrue(ret.Successful(), $"Create NFT failed: {ret.ResultMessage}");
        }

        private async Task BurnAllNFT()
        {
            // burn all NFT.
            var lastblk = testWallet.GetLastSyncBlock();
            foreach (var kvp in lastblk.Balances)
            {
                if (kvp.Key.StartsWith("nft/"))
                {
                    var burnret = await testWallet.SendAsync(kvp.Value.ToBalanceDecimal(), LyraGlobal.BURNINGACCOUNTID, kvp.Key);
                    Assert.IsTrue(burnret.Successful());
                }
            }
        }
    }
}
