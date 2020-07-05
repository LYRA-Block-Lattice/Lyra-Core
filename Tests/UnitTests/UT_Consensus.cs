using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.Xunit2;
using FluentAssertions;
using Lyra;
using Lyra.Core.Accounts;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.Network.P2P;
using Newtonsoft.Json;
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

        private DagSystem TheDagSystem;
        private TestProbe fakeP2P;

        [TestInitialize]
        public void TestSetup()
        {
            SimpleLogger.Factory = new NullLoggerFactory();

            var mockStore = new Mock<IAccountCollectionAsync>();
            var posWallet = Restore("25kksnE589CTHcDeMNbatGBGoCjiMNFzcDCuGULj1vgCMAfxNV");

            fakeP2P = CreateTestProbe();
            TheDagSystem = new DagSystem("xtest", mockStore.Object, posWallet, fakeP2P);

            fakeP2P.SetAutoPilot(new DelegateAutoPilot((sender, message) =>
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

        [TestCleanup]
        public void Cleanup()
        {
            Shutdown();
        }

        [TestMethod]
        public void ConsensusServiceTest()
        {
            //var ln = Sys.ActorOf(Props.Create(() => new LocalNode(TestBlockChain.TheDagSystem)));
            var cs = Sys.ActorOf(Props.Create(() => new ConsensusService(fakeP2P)));

            // NodeInquiry
            var inq = new ChatMsg("", ChatMessageType.NodeStatusInquiry);
            cs.Tell(new LocalNode.SignedMessageRelay { signedMessage = inq });
            var msg = fakeP2P.FishForMessage<ChatMsg>(a => a.MsgType == ChatMessageType.NodeStatusReply, TimeSpan.FromMinutes(5));
            msg.Should().NotBeNull();
            var inqResult = JsonConvert.DeserializeObject<NodeStatus>(msg.Text);
            inqResult.totalBlockCount.Should().Be(0);

            cs.Tell(new LocalNode.SignedMessageRelay { signedMessage = msg });

            // 

        }

        [TestMethod]
        public void WalletRestoreTest()
        {
            var wallet1 = Restore("");
            wallet1.Should().BeNull();

            var wallet2 = Restore(PRIVATE_KEY_1);
            wallet2.AccountId.Should().Be(ADDRESS_ID_1);
        }

        private Wallet Restore(string privateKey)
        {
            var memStor = new AccountInMemoryStorage();
            var acctWallet = new ExchangeAccountWallet(memStor, LyraNodeConfig.GetNetworkId());
            acctWallet.AccountName = "tmpAcct";
            var result = acctWallet.RestoreAccount("", privateKey);
            if (result.ResultCode == Lyra.Core.Blocks.APIResultCodes.Success)
            {
                acctWallet.OpenAccount("", acctWallet.AccountName);
                return acctWallet;
            }
            else
            {
                return null;
            }
        }
    }
}
