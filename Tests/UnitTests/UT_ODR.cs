using Lyra.Core.Blocks;
using Lyra.Data.API.WorkFlow;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_ODR : XTestBase
    {
        [TestMethod]
        public async Task TestODR()
        {
            await SetupWallets("devnet");

            await CreateOrder();
        }

        private async Task CreateOrder()
        {
            var crypto = "unittest/ETH";

            await testWallet.SyncAsync(null);
            if (!testWallet.GetLastSyncBlock().Balances.ContainsKey(crypto))
            {
                // init. create token to sell
                var tokenGenesisResult = await testWallet.CreateTokenAsync("ETH", "unittest", "", 8, 100000, false, testWallet.AccountId,
                        "", "", ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(tokenGenesisResult.Successful(), $"test otc token genesis failed: {tokenGenesisResult.ResultCode} for {testWallet.AccountId}");

                await testWallet.SyncAsync(null);

                await testWallet.SendAsync(100, test2PublicKey, crypto);
                await test2Wallet.SyncAsync(null);
            }

            Assert.IsTrue(testWallet.GetLastSyncBlock().Balances.ContainsKey(crypto));

            // first create a DAO
            var name = "First DAO";
            var desc = "Doing great business!";
            var daoret = await testWallet.RPC.GetDaoByNameAsync(name);
            if (!daoret.Successful())
            {
                var dcret = await testWallet.CreateDAOAsync(name, desc, 1, 0.01m, 0.001m, 10, 120, 130);
                Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");
            }
            var dao = daoret.GetBlock() as IDao;
            Assert.AreEqual(name, dao.Name);
        }
    }
}
