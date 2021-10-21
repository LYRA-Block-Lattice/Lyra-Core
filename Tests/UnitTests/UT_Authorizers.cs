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

        private TransactionBlock _lastBlock;

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

        private async Task AuthAsync(Block block)
        {
            var auth = af.Create(block.BlockType);
            var result = await auth.AuthorizeAsync(sys, block);
            Assert.IsTrue(result.Item1 == Lyra.Core.Blocks.APIResultCodes.Success, $"{result.Item1}");
        }

        [TestMethod]
        public async Task FullTest()
        {
            while (cs == null)
            {
                await Task.Delay(1000);
                cs = ConsensusService.Instance;                
            }
            cs.Board.CurrentLeader = sys.PosWallet.AccountId;
            cs.Board.LeaderCandidate = sys.PosWallet.AccountId;
            ProtocolSettings.Default.StandbyValidators[0] = cs.Board.CurrentLeader;

            var svcGen = await cs.CreateServiceGenesisBlockAsync();
            //await AuthAsync(svcGen);
            await store.AddBlockAsync(svcGen);
            var tokenGen = cs.CreateLyraTokenGenesisBlock(svcGen);
            await AuthAsync(tokenGen);
            await store.AddBlockAsync(tokenGen);
            var pf = await cs.CreatePoolFactoryBlockAsync();
            await AuthAsync(pf);
            await store.AddBlockAsync(pf);
            var consGen = cs.CreateConsolidationGenesisBlock(svcGen, tokenGen, pf);
            //await AuthAsync(consGen);
            await store.AddBlockAsync(consGen);

            NodeService.Dag = sys;
            var api = new NodeAPI();
            var apisvc = new ApiService(NullLogger<ApiService>.Instance);
            var mock = new Mock<ILyraAPI>();
            mock.Setup(x => x.SendTransferAsync(It.IsAny<SendTransferBlock>()))
                .Callback((SendTransferBlock block) => {
                    var t = Task.Run(async () => {
                        await AuthAsync(block);
                        await store.AddBlockAsync(block);
                    });
                    Task.WaitAll(t);
                })
                .ReturnsAsync(new AuthorizationAPIResult { ResultCode = APIResultCodes.Success });
            mock.Setup(x => x.GetSyncHeightAsync())
                .ReturnsAsync(await api.GetSyncHeightAsync());
            mock.Setup(x => x.GetLastServiceBlockAsync())
                .ReturnsAsync(await api.GetLastServiceBlockAsync());

            mock.Setup(x => x.GetLastBlockAsync(It.IsAny<string>()))
                //.Callback((string s) => accId = s)
                .Returns<string>(acct => Task.FromResult(api.GetLastBlockAsync(acct)).Result);
            mock.Setup(x => x.LookForNewTransfer2Async(It.IsAny<string>(), It.IsAny<string>()))
                //.Callback((string a, string b) => { accId = a; sign = b; })
                //.ReturnsAsync(await api.LookForNewTransfer2Async(accId, sign));
                .Returns<string, string>((acct, sign) => Task.FromResult(api.LookForNewTransfer2Async(acct, sign)).Result);

            mock.Setup(x => x.ReceiveTransferAsync(It.IsAny<ReceiveTransferBlock>()))
                .Callback((ReceiveTransferBlock block) => {
                    var t = Task.Run(async () => {
                        await AuthAsync(block);
                        await store.AddBlockAsync(block);
                    });
                    Task.WaitAll(t);
                })
                .ReturnsAsync(new AuthorizationAPIResult { ResultCode = APIResultCodes.Success });
            mock.Setup(x => x.ReceiveTransferAndOpenAccountAsync(It.IsAny<OpenWithReceiveTransferBlock>()))
                .Callback((OpenWithReceiveTransferBlock block) => {
                    var t = Task.Run(async () => {
                        await AuthAsync(block);
                        await store.AddBlockAsync(block);
                    });
                    Task.WaitAll(t);
                })
                .ReturnsAsync(new AuthorizationAPIResult { ResultCode = APIResultCodes.Success });

            var walletStor = new AccountInMemoryStorage();
            Wallet.Create(walletStor, "gensisi", "1234", "xtest", sys.PosWallet.PrivateKey);

            genesisWallet = Wallet.Open(walletStor, "gensisi", "1234", mock.Object);
            await genesisWallet.SyncAsync(mock.Object);

            Assert.IsTrue(genesisWallet.BaseBalance > 100000000m);

            var sendResult = await genesisWallet.SendAsync(10000m, testPublicKey);
            Assert.IsTrue(sendResult.Successful(), $"send error {sendResult.ResultCode}");

            var walletStor2 = new AccountInMemoryStorage();
            Wallet.Create(walletStor2, "xunit", "1234", "xtest", testPrivateKey);
            testWallet = Wallet.Open(walletStor2, "xunit", "1234", mock.Object);
            Assert.AreEqual(testWallet.AccountId, testPublicKey);

            await testWallet.SyncAsync(mock.Object);
            Assert.AreEqual(testWallet.BaseBalance, 10000m);
        }
    }
}
