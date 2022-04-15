using DexServer.Ext;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_DexClient
    {
        private string networkId = TestConfig.networkId;

        ILyraAPI client;

        [TestMethod]
        public async Task TestGenerateWalletAsync()
        {
            var (pvtx, pubx) = Signatures.GenerateWallet();

            // first get some coin
            var gens = await GetGenesisWalletAsync();
            var dexwallet = await GetWalletAsync(pvtx);

            await gens.SendAsync(100, dexwallet.AccountId);
            await dexwallet.SyncAsync(null);
            Assert.AreEqual(100, dexwallet.BaseBalance);

            // request a wallet
            var crret = await dexwallet.CreateDexWalletAsync("TRX", "native");
            Assert.IsTrue(crret.Successful());
            await Task.Delay(2000);

            //var dc = new DexClient(networkId);
            //var r1 = await dc.CreateWalletAsync(pubx, "TRX", "native", "", "", "");
            //Assert.IsTrue(r1.Success);
            //var extw = r1 as DexAddress;
            //Assert.IsTrue(extw.Address.StartsWith('T'));
        }
        
        private async Task<Wallet> GetGenesisWalletAsync()
        {
            string lyra_folder = Wallet.GetFullFolderName(networkId, "wallets");
            var storage = new SecuredWalletStore(lyra_folder);
            var wallet = Wallet.Open(storage, "vault", "");

            client = LyraRestClient.Create(networkId, "windows", "unit test", "1.0");
            await wallet.SyncAsync(client);

            Assert.IsTrue(wallet.BaseBalance > 1000000m);
            return wallet;
        }

        private async Task<Wallet> GetWalletAsync(string privateKey)
        {
            var walletStor = new AccountInMemoryStorage();
            Wallet.Create(walletStor, "gensisi", "1234", networkId, privateKey);

            var w = Wallet.Open(walletStor, "gensisi", "1234", client);

            return w;
        }

        /*
        private async Task TestDepositWithdraw()
        {
            // prepare dex
            string lyrawalletfolder = Wallet.GetFullFolderName(networkId, "wallets");
            var walletStore = new SecuredWalletStore(lyrawalletfolder);
            var dexWallet = Wallet.Open(walletStore, "dex", "");
            await genesisWallet.SendAsync(100000m, dexWallet.AccountId);
            await Task.Delay(1000);
            await dexWallet.SyncAsync(genesisWallet.RPC);
            Assert.IsTrue(dexWallet.BaseBalance >= 100000m);

            // external token genesis
            var tgexists = await client.GetTokenGenesisBlockAsync(null, "tether/TRX", null);
            if (!tgexists.Successful())
            {
                var tokenGenesisResult = await dexWallet.CreateTokenAsync("TRX", "tether", "", 8, 0, false, dexWallet.AccountId,
                        "", "", ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(tokenGenesisResult.Successful(), "dex token genesis failed");
            }

            // create dex wallet
            await testWallet.SyncAsync(null);
            var crdexret = await testWallet.CreateDexWalletAsync("TRX", "native");
            Assert.IsTrue(crdexret.Successful());

            await WaitWorkflow("CreateDexWalletAsync");

            var dexws = await testWallet.GetAllDexWalletsAsync(testWallet.AccountId);
            Assert.IsNotNull(dexws, "DEX Wallet not setup.");
            var wcnt = dexws.Count(a => (a as IDexWallet).ExtSymbol == "TRX" && (a as IDexWallet).ExtProvider == "native");
            Assert.AreEqual(1, wcnt, $"wallet not created properly. created: {wcnt}");

            // must fail
            //await testWallet.SyncAsync(null);
            //var getokretx = await testWallet.DexGetTokenAsync((dexws.First() as TransactionBlock).AccountID, 500m);
            //Assert.IsTrue(!getokretx.Successful(), "Should not success");

            // mint
            var dexbrk1 = dexws.First() as TransactionBlock;
            var mintRet = await dexWallet.DexMintTokenAsync(dexbrk1.AccountID, 1000m);
            Assert.IsTrue(mintRet.Successful(), "Mint failed.");
            await Task.Delay(1000);

            var brk1lstret = await client.GetLastBlockAsync(dexbrk1.AccountID);
            Assert.IsTrue(brk1lstret.Successful());
            var brk1mint = brk1lstret.GetBlock() as TokenMintBlock;
            Assert.IsNotNull(brk1mint);

            if (networkId == "xunit")
            {
                Assert.AreEqual(2, brk1mint.Height, "No mint block generated.");
                Assert.AreEqual(1000, brk1mint.Balances["tether/TRX"].ToBalanceDecimal());
            }

            // get minted token to owner wallet
            await testWallet.SyncAsync(null);
            var getokret = await testWallet.DexGetTokenAsync(dexbrk1.AccountID, 500m);
            Assert.IsTrue(getokret.Successful(), "error get ext token to own wallet");
            await Task.Delay(1500);
            await testWallet.SyncAsync(null);
            Assert.AreEqual(500m, testWallet.GetLastSyncBlock().Balances["tether/TRX"].ToBalanceDecimal(), "Ext token amount error");

            // put external token to dex wallet
            var putokret = await testWallet.DexPutTokenAsync(dexbrk1.AccountID, "tether/TRX", 500m);
            Assert.IsTrue(putokret.Successful(), "Put token error");
            await Task.Delay(1500);
            var brk1lstret2 = await client.GetLastBlockAsync(dexbrk1.AccountID);
            Assert.IsTrue(brk1lstret2.Successful());
            var brk1lastblk = brk1lstret2.GetBlock() as TransactionBlock;
            if (networkId == "xunit")
            {
                Assert.AreEqual(1000m, brk1lastblk.Balances["tether/TRX"].ToBalanceDecimal(), "brk1 ext tok balance error");
            }

            // withdraw token to external blockchain
            var wdwret = await testWallet.DexWithdrawTokenAsync(dexbrk1.AccountID, "Txxxxxxxxx", 1000m);
            Assert.IsTrue(wdwret.Successful(), "Error withdraw");
            await Task.Delay(1500);
            var brk1lstret3 = await client.GetLastBlockAsync(dexbrk1.AccountID);
            Assert.IsTrue(brk1lstret3.Successful());
            var brk1lastblk3 = brk1lstret3.GetBlock() as TokenBurnBlock;
            if (networkId == "xunit")
                Assert.AreEqual(0m, brk1lastblk3.Balances["tether/TRX"].ToBalanceDecimal(), "brk1 ext tok burn error");

        }*/
    }
}
