using Akka.Actor;
using Akka.TestKit.Xunit2;
using FluentAssertions;
using Lyra.Core.Accounts;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.P2P;
using System;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_Consensus : TestKit
    {
        const string PRIVATE_KEY_1 = "25kksnE589CTHcDeMNbatGBGoCjiMNFzcDCuGULj1vgCMAfxNV"; // merchant
        const string PRIVATE_KEY_2 = "2QvkckNTBttTt9EwsvWhDCwibcvzSkksx5iBuikh1AzgdYsNov"; // customer

        const string ADDRESS_ID_1 = "L4hksrWP5pzQ4pdDdUZ4D9GgZoT3iGZiaWNgcTPjSUATyogyJaZk1qYHfKuMnTytqfqEp3fgWQ7NxoQXVZPykqj2ALWejo";
        const string ADDRESS_ID_2 = "LPR9pZeLhB4eHHuQBEDLTVoAJUZUWNbfux2QpSvK6vJbcXsGK6Rz3gN3ynNixcz9yAaA9iLCEJ7c5oQobQpUS66vPtZ2Yq";

        [TestInitialize]
        public void TestSetup()
        {
            TestBlockChain.InitializeMockDagSystem();
        }

        [TestCleanup]
        public void Cleanup()
        {
            Shutdown();
        }

        [TestMethod]
        public void WalletRestoreTest()
        {
            var wallet1 = TestBlockChain.Restore("");
            wallet1.Should().BeNull();

            var wallet2 = TestBlockChain.Restore(PRIVATE_KEY_1);
            wallet2.AccountId.Should().Be(ADDRESS_ID_1);
        }

        [TestMethod]
        public void ConsensusServiceTest()
        {
            var probe = CreateTestProbe();

            //var ln = Sys.ActorOf(Props.Create(() => new LocalNode(TestBlockChain.TheDagSystem)));
            var cs = Sys.ActorOf(Props.Create(() => new ConsensusService(probe)));

            // NodeInquiry
            cs.Tell(new ConsensusService.NodeInquiry());
            probe.ExpectMsg<SourceSignedMessage>(TimeSpan.FromSeconds(1000));
        }
    }
}
