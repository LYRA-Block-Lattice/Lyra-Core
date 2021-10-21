using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.Xunit2;
using FluentAssertions;
using Lyra;
using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
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
    public class UT_Consensus : TestKit
    {
        //private const int NodesCount = 4;
        //private TestProbe[] p2pStacks;
        //private TestAuthorizer[] authorizers;

        private ConsensusService cs;
        private IAccountCollectionAsync store;
        private AuthorizersFactory af;
        private DagSystem sys;

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

            //p2pStacks = new TestProbe[NodesCount];
            //authorizers = new TestAuthorizer[NodesCount];
            //for(int i = 0; i < NodesCount; i++)
            //{
            //    p2pStacks[i] = CreateTestProbe();
            //    authorizers[i] = new TestAuthorizer(p2pStacks[i]);

            //    p2pStacks[i].SetAutoPilot(new DelegateAutoPilot((sender, message) =>
            //    {
            //        var msg = message as LocalNode.SignedMessageRelay;
            //        if (msg != null)
            //        {
            //            // foreach dagsys sender not same tell it
            //        }
            //        sender.Tell(message, ActorRefs.NoSender);
            //        return AutoPilot.KeepRunning;
            //    }));
            //}
        }

        [TestCleanup]
        public void Cleanup()
        {
            store.Delete(true);
            Shutdown();
        }

        private async Task AuthAsync(Block block)
        {
            var auth = af.Create(block.BlockType);
            var result = await auth.AuthorizeAsync(sys, block);
            Assert.IsTrue(result.Item1 == Lyra.Core.Blocks.APIResultCodes.Success, $"{result.Item1}");
        }

        [TestMethod]
        public async Task GenesisTest()
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
        }

        [TestMethod]
        public void ConsensusServiceTest()
        {
            ////var ln = Sys.ActorOf(Props.Create(() => new LocalNode(TestBlockChain.TheDagSystem)));
            //var cs = Sys.ActorOf(Props.Create(() => new ConsensusService(fakeP2P)));

            //// NodeInquiry
            //var inq = new ChatMsg("", ChatMessageType.NodeStatusInquiry);
            //cs.Tell(new LocalNode.SignedMessageRelay { signedMessage = inq });
            //var msg = fakeP2P.FishForMessage<ChatMsg>(a => a.MsgType == ChatMessageType.NodeStatusReply, TimeSpan.FromMinutes(5));
            //msg.Should().NotBeNull();
            //var inqResult = JsonConvert.DeserializeObject<NodeStatus>(msg.Text);
            //inqResult.totalBlockCount.Should().Be(0);

            //cs.Tell(new LocalNode.SignedMessageRelay { signedMessage = msg });

            // 

        }




    }
}
