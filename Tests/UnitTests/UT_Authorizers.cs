using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.Xunit2;
using FluentAssertions;
using Lyra;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo;
using Neo.Network.P2P;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_Authorizers : TestKit
    {
        readonly string testPrivateKey = "2LqBaZopCiPjBQ9tbqkqqyo4TSaXHUth3mdMJkhaBbMTf6Mr8u";
        readonly string testPublicKey = "LUTPLGNAP4vTzXh5tWVCmxUBh8zjGTR8PKsfA8E67QohNsd1U6nXPk4Q9jpFKsKfULaaT3hs6YK7WKm57QL5oarx8mZdbM";

        private ConsensusService cs;
        private IAccountCollectionAsync store;
        private AuthorizersFactory af;
        private DagSystem sys;

        private Wallet genesisWallet;
        private Wallet testWallet;

        ILyraAPI client;

        [TestInitialize]
        public void TestSetup()
        {
            SimpleLogger.Factory = new NullLoggerFactory();

            var probe = CreateTestProbe();
            var ta = new TestAuthorizer(probe);
            sys = ta.TheDagSystem;
            sys.StartConsensus();
            store = ta.TheDagSystem.Storage;

            af = new AuthorizersFactory();
            af.Init();
        }

        [TestCleanup]
        public void Cleanup()
        {
            //store.Delete(true);
            Shutdown();
        }

        private async Task<bool> AuthAsync(Block block)
        {
            var auth = af.Create(block.BlockType);
            var result = await auth.AuthorizeAsync(sys, block);
            Assert.IsTrue(result.Item1 == Lyra.Core.Blocks.APIResultCodes.Success, $"{result.Item1}");

            await store.AddBlockAsync(block);

            cs.Worker_OnConsensusSuccess(block, ConsensusResult.Yea, true);

            return result.Item1 == Lyra.Core.Blocks.APIResultCodes.Success;
        }

        [TestMethod]
        public async Task FullTest()
        {
            while (cs == null)
            {
                await Task.Delay(1000);
                cs = ConsensusService.Instance;                
            }
            cs.OnNewBlock += async (b) => (ConsensusResult.Yea, await AuthAsync(b) ? APIResultCodes.Success : APIResultCodes.UndefinedError);
            //{
            //    var result = ;
            //    //return Task.FromResult( (ConsensusResult.Yea, result) );
            //}
            cs.Board.CurrentLeader = sys.PosWallet.AccountId;
            cs.Board.LeaderCandidate = sys.PosWallet.AccountId;
            ProtocolSettings.Default.StandbyValidators[0] = cs.Board.CurrentLeader;

            var svcGen = await cs.CreateServiceGenesisBlockAsync();
            //await AuthAsync(svcGen);
            await store.AddBlockAsync(svcGen);
            var tokenGen = cs.CreateLyraTokenGenesisBlock(svcGen);
            await AuthAsync(tokenGen);
            var pf = await cs.CreatePoolFactoryBlockAsync();
            await AuthAsync(pf);
            var consGen = cs.CreateConsolidationGenesisBlock(svcGen, tokenGen, pf);
            //await AuthAsync(consGen);
            await store.AddBlockAsync(consGen);

            NodeService.Dag = sys;
            var api = new NodeAPI();
            var apisvc = new ApiService(NullLogger<ApiService>.Instance);
            var mock = new Mock<ILyraAPI>();
            client = mock.Object;
            mock.Setup(x => x.SendTransferAsync(It.IsAny<SendTransferBlock>()))
                .Callback((SendTransferBlock block) => {
                    var t = Task.Run(async () => {
                        await AuthAsync(block);
                    });
                    Task.WaitAll(t);
                })
                .ReturnsAsync(new AuthorizationAPIResult { ResultCode = APIResultCodes.Success });
            mock.Setup(x => x.GetSyncHeightAsync())
                .ReturnsAsync(await api.GetSyncHeightAsync());
            mock.Setup(x => x.GetLastServiceBlockAsync())
                .ReturnsAsync(await api.GetLastServiceBlockAsync());

            mock.Setup(x => x.GetLastBlockAsync(It.IsAny<string>()))
                .Returns<string>(acct => Task.FromResult(api.GetLastBlockAsync(acct)).Result);
            mock.Setup(x => x.GetBlockBySourceHashAsync(It.IsAny<string>()))
                .Returns<string>(acct => Task.FromResult(api.GetBlockBySourceHashAsync(acct)).Result);
            mock.Setup(x => x.GetBlocksByRelatedTxAsync(It.IsAny<string>()))
                .Returns<string>(acct => Task.FromResult(api.GetBlocksByRelatedTxAsync(acct)).Result);
            mock.Setup(x => x.LookForNewTransfer2Async(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((acct, sign) => Task.FromResult(api.LookForNewTransfer2Async(acct, sign)).Result);
            mock.Setup(x => x.GetTokenGenesisBlockAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string, string>((acct, token, sign) => Task.FromResult(api.GetTokenGenesisBlockAsync(acct, token, sign)).Result);
            mock.Setup(x => x.GetPoolAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((acct, sign) => Task.FromResult(api.GetPoolAsync(acct, sign)).Result);

            mock.Setup(x => x.ReceiveTransferAsync(It.IsAny<ReceiveTransferBlock>()))
                .Callback((ReceiveTransferBlock block) => {
                    var t = Task.Run(async () => {
                        await AuthAsync(block);
                    });
                    Task.WaitAll(t);
                })
                .ReturnsAsync(new AuthorizationAPIResult { ResultCode = APIResultCodes.Success });
            mock.Setup(x => x.ReceiveTransferAndOpenAccountAsync(It.IsAny<OpenWithReceiveTransferBlock>()))
                .Callback((OpenWithReceiveTransferBlock block) => {
                    var t = Task.Run(async () => {
                        await AuthAsync(block);
                    });
                    Task.WaitAll(t);
                })
                .ReturnsAsync(new AuthorizationAPIResult { ResultCode = APIResultCodes.Success });
            mock.Setup(x => x.CreateTokenAsync(It.IsAny<TokenGenesisBlock>()))
                .Callback((TokenGenesisBlock block) => {
                    var t = Task.Run(async () => {
                        await AuthAsync(block);
                    });
                    Task.WaitAll(t);
                })
                .ReturnsAsync(new AuthorizationAPIResult { ResultCode = APIResultCodes.Success });
            var walletStor = new AccountInMemoryStorage();
            Wallet.Create(walletStor, "gensisi", "1234", "xtest", sys.PosWallet.PrivateKey);

            genesisWallet = Wallet.Open(walletStor, "gensisi", "1234", mock.Object);
            await genesisWallet.SyncAsync(mock.Object);

            Assert.IsTrue(genesisWallet.BaseBalance > 100000000m);

            var tamount = 1000000m;
            var sendResult = await genesisWallet.SendAsync(tamount, testPublicKey);
            Assert.IsTrue(sendResult.Successful(), $"send error {sendResult.ResultCode}");

            var walletStor2 = new AccountInMemoryStorage();
            Wallet.Create(walletStor2, "xunit", "1234", "xtest", testPrivateKey);
            testWallet = Wallet.Open(walletStor2, "xunit", "1234", mock.Object);
            Assert.AreEqual(testWallet.AccountId, testPublicKey);

            await testWallet.SyncAsync(mock.Object);
            Assert.AreEqual(testWallet.BaseBalance, tamount);

            //await TestPoolAsync();
            await TestProfitingAndStaking();

            // let workflow to finish
            await Task.Delay(3000);
        }

        private async Task TestProfitingAndStaking()
        {
            var crpftret = await testWallet.CreateProfitingAccountAsync("moneycow", ProfitingType.Node,
                0.5m, 50);
            Assert.IsTrue(crpftret.Successful());
            var pftblock = crpftret.GetBlock() as ProfitingBlock;
            Assert.IsTrue(pftblock.AccountID.StartsWith('L'));
        }

        private async Task TestPoolAsync()
        {
            // create pool
            var token0 = "unnitest/test0";
            var token1 = "unnitest/test1";
            var secs0 = token0.Split('/');
            var result0 = await testWallet.CreateTokenAsync(secs0[1], secs0[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
            Assert.IsTrue(result0.Successful(), "Failed to create token: " + result0.ResultCode);
            await testWallet.SyncAsync(null);

            var secs1 = token1.Split('/');
            var result1 = await testWallet.CreateTokenAsync(secs1[1], secs1[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
            Assert.IsTrue(result0.Successful(), "Failed to create token: " + result1.ResultCode);
            await testWallet.SyncAsync(null);

            var crplret = await testWallet.CreateLiquidatePoolAsync(token0, "LYR");
            Assert.IsTrue(crplret.Successful());
            while (true)
            {
                var pool = await testWallet.GetLiquidatePoolAsync(token0, "LYR");
                if (pool.PoolAccountId == null)
                {
                    await Task.Delay(100);
                    continue;
                }
                Assert.IsTrue(pool.PoolAccountId.StartsWith('L'));
                break;
            }

            // add liquidate to pool
            var addpoolret = await testWallet.AddLiquidateToPoolAsync(token0, 1000000, "LYR", 5000);
            Assert.IsTrue(addpoolret.Successful());

            await Task.Delay(1000);

            // swap
            var poolx = await client.GetPoolAsync(token0, LyraGlobal.OFFICIALTICKERCODE);
            Assert.IsNotNull(poolx.PoolAccountId);
            var poolLatestBlock = poolx.GetBlock() as TransactionBlock;

            var cal2 = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, token0, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, 20, 0);
            var swapret = await testWallet.SwapTokenAsync("LYR", token0, "LYR", 20, cal2.SwapOutAmount);
            Assert.IsTrue(swapret.Successful());

            await Task.Delay(1000);

            // remove liquidate from pool
            var rmliqret = await testWallet.RemoveLiquidateFromPoolAsync(token0, "LYR");
            Assert.IsTrue(rmliqret.Successful());

            await Task.Delay(1000);
            await testWallet.SyncAsync(null);
        }
    }
}
