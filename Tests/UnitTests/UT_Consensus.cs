using Akka.TestKit.Xunit2;
using FluentAssertions;
using Lyra.Core.Accounts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_Consensus : TestKit
    {
        const string PRIVATE_KEY_1 = "25kksnE589CTHcDeMNbatGBGoCjiMNFzcDCuGULj1vgCMAfxNV"; // merchant
        const string PRIVATE_KEY_2 = "2QvkckNTBttTt9EwsvWhDCwibcvzSkksx5iBuikh1AzgdYsNov"; // customer

        const string NETWORK_ID = "unittest";

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
        public async Task TestMethod1Async()
        {
            // create wallet and update balance
            var memStor = new AccountInMemoryStorage();
            var acctWallet = new ExchangeAccountWallet(memStor, NETWORK_ID);
            acctWallet.AccountName = "tmpAcct";
            await acctWallet.RestoreAccountAsync("", PRIVATE_KEY_1);
            acctWallet.OpenAccount("", acctWallet.AccountName);

            acctWallet.AccountId.Should().StartWith("L");
            //Console.WriteLine("Sync wallet for " + acctWallet.AccountId);
            //var rpcClient = await LyraRestClient.CreateAsync(Neo.Settings.Default.LyraNode.Lyra.NetworkId, Environment.OSVersion.Platform.ToString(), "WizDAG Client Cli", "1.0a");
            //await acctWallet.Sync(rpcClient);
        }
    }
}
