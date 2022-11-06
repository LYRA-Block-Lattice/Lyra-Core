﻿using Lyra.Core.Accounts;
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

        }
    }
}