using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.Xunit2;
using FluentAssertions;
using Lyra;
using Lyra.Core.Accounts;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Lyra.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
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
        private const int NodesCount = 4;
        private TestProbe[] p2pStacks;
        private TestAuthorizer[] authorizers;

        [TestInitialize]
        public void TestSetup()
        {
            SimpleLogger.Factory = new NullLoggerFactory();

            p2pStacks = new TestProbe[NodesCount];
            authorizers = new TestAuthorizer[NodesCount];
            for(int i = 0; i < NodesCount; i++)
            {
                p2pStacks[i] = CreateTestProbe();
                authorizers[i] = new TestAuthorizer(p2pStacks[i]);

                p2pStacks[i].SetAutoPilot(new DelegateAutoPilot((sender, message) =>
                {
                    var msg = message as LocalNode.SignedMessageRelay;
                    if (msg != null)
                    {
                        // foreach dagsys sender not same tell it
                    }
                    sender.Tell(message, ActorRefs.NoSender);
                    return AutoPilot.KeepRunning;
                }));
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            Shutdown();
        }

        [TestMethod]
        public void DagStatus()
        {
            
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
