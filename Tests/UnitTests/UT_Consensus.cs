using Akka.TestKit.Xunit2;
using FluentAssertions;
using Lyra.Core.Accounts;
using Lyra.Core.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        [TestMethod]
        public void WalletRestore()
        {
            // create wallet and update balance
            var wallet1 = Restore(PRIVATE_KEY_1);
            wallet1.AccountId.Should().Be(ADDRESS_ID_1);

            //Console.WriteLine("Sync wallet for " + acctWallet.AccountId);
            //var rpcClient = await LyraRestClient.CreateAsync(Neo.Settings.Default.LyraNode.Lyra.NetworkId, Environment.OSVersion.Platform.ToString(), "WizDAG Client Cli", "1.0a");
            //await acctWallet.Sync(rpcClient);
        }

        [TestMethod]
        public void WalletRestore2()
        {
            // create wallet and update balance
            var wallet1 = Restore("");
            wallet1.Should().BeNull();

            //Console.WriteLine("Sync wallet for " + acctWallet.AccountId);
            //var rpcClient = await LyraRestClient.CreateAsync(Neo.Settings.Default.LyraNode.Lyra.NetworkId, Environment.OSVersion.Platform.ToString(), "WizDAG Client Cli", "1.0a");
            //await acctWallet.Sync(rpcClient);
        }
    }
}
