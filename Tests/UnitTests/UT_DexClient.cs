using DexServer.Ext;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
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
            var dexUserWallet = await GetWalletAsync(pvtx);

            await dexUserWallet.SetupEventsListenerAsync();

            await gens.SendAsync(100, dexUserWallet.AccountId);
            await dexUserWallet.SyncAsync(null);
            Assert.AreEqual(100, dexUserWallet.BaseBalance);

            // request a wallet
            var crret = await dexUserWallet.CreateDexWalletAsync("TRX", "native");
            Assert.IsTrue(crret.Successful(), $"dex can't create wallet: {crret.ResultCode} {crret.ResultMessage}");
            Assert.IsTrue(dexUserWallet.WaitForWorkflow(crret.TxHash));

            await TestDepositWithdraw(dexUserWallet);
        }

        [TestMethod]
        public async Task TestGetStatus()
        {
            var wallet = await GetGenesisWalletAsync();

            client = LyraRestClient.Create(networkId, "windows", "unit test", "1.0");
            var lsb = await client.GetLastServiceBlockAsync();
            Assert.IsTrue(lsb.Successful());

            var dexc = new DexClient(networkId);
            var signatures = Signatures.GetSignature(wallet.PrivateKey, lsb.GetBlock().Hash, wallet.AccountId);
            var ret = await dexc.GetDexStatusAsync(wallet.AccountId, signatures);
            Assert.IsTrue(ret.Online);
        }
        
        private async Task<Wallet> GetGenesisWalletAsync()
        {
            string lyra_folder = Wallet.GetFullFolderName(networkId, "wallets");
            var storage = new SecuredWalletStore(lyra_folder);
            var wallet = Wallet.Open(storage, "vault", "");

            client = LyraRestClient.Create(networkId, "windows", "unit test", "1.0");
            await wallet.SyncAsync(client);

            Assert.IsTrue(wallet.BaseBalance > 100000m);
            return wallet;
        }

        private async Task<Wallet> GetWalletAsync(string privateKey)
        {
            var walletStor = new AccountInMemoryStorage();
            Wallet.Create(walletStor, "gensisi", "1234", networkId, privateKey);

            var w = Wallet.Open(walletStor, "gensisi", "1234", client);

            return w;
        }

        private async Task TestDepositWithdraw(Wallet dexUserWallet)
        {
            // prepare dex
            string lyrawalletfolder = Wallet.GetFullFolderName(networkId, "wallets");
            var walletStore = new SecuredWalletStore(lyrawalletfolder);
            var dexWallet = Wallet.Open(walletStore, "dex", "");
            var genesisWallet = await GetGenesisWalletAsync();
            var ret = await genesisWallet.SendAsync(100000m, dexWallet.AccountId);

            await dexWallet.SyncAsync(genesisWallet.RPC);
            Assert.IsTrue(dexWallet.BaseBalance >= 100000m);

            // external token genesis
            var lsbret = await client.GetLastServiceBlockAsync();
            Assert.IsTrue(lsbret.Successful());
            var lsb = lsbret.GetBlock();

            var tgexists = await client.GetTokenGenesisBlockAsync(dexWallet.AccountId, "tether/TRX",
                Signatures.GetSignature(dexWallet.PrivateKey, lsb.Hash, dexWallet.AccountId));
            if (!tgexists.Successful())
            {
                var tokenGenesisResult = await dexWallet.CreateTokenAsync("TRX", "tether", "", 8, 0, false, dexWallet.AccountId,
                        "", "", ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(tokenGenesisResult.Successful(), "dex token genesis failed");
            }

            var dexws = await dexUserWallet.GetAllDexWalletsAsync(dexUserWallet.AccountId);
            Assert.IsNotNull(dexws, "DEX Wallet not setup.");
            var wcnt = dexws.Count(a => a.ExtSymbol == "TRX" && a.ExtProvider == "native");
            Assert.AreEqual(1, wcnt, $"wallet not created properly. created: {wcnt}");

            // must fail
            await dexUserWallet.SyncAsync(null);
            var getokretx = await dexUserWallet.DexGetTokenAsync((dexws.First() as TransactionBlock).AccountID, 500m);
            Assert.IsTrue(getokretx.Successful(), "Should not not success");
            Assert.IsTrue(dexUserWallet.WaitForWorkflow(getokretx.TxHash));

            // mint via dex account
            var dexbrk1 = dexws.First() as TransactionBlock;
            var mintRet = await dexWallet.DexMintTokenAsync(dexbrk1.AccountID, 1000m);
            Assert.IsTrue(mintRet.Successful(), $"Mint failed: {mintRet.ResultCode} {mintRet.ResultMessage}");
            Assert.IsTrue(dexUserWallet.WaitForWorkflow(mintRet.TxHash));

            var brk1lstret = await client.GetLastBlockAsync(dexbrk1.AccountID);
            Assert.IsTrue(brk1lstret.Successful());
            var brk1mintblk = brk1lstret.GetBlock();
            var brk1mint = brk1mintblk as TokenMintBlock;
            Assert.IsNotNull(brk1mint);

            if (networkId == "xunit")
            {
                Assert.AreEqual(2, brk1mint.Height, "No mint block generated.");
                Assert.AreEqual(1000, brk1mint.Balances["tether/TRX"].ToBalanceDecimal());
            }

            // get minted token to owner wallet
            await dexUserWallet.SyncAsync(null);
            var getokret = await dexUserWallet.DexGetTokenAsync(dexbrk1.AccountID, 500m);
            Assert.IsTrue(getokret.Successful(), "error get ext token to own wallet");
            Assert.IsTrue(dexUserWallet.WaitForWorkflow(getokret.TxHash));

            await dexUserWallet.SyncAsync(null);
            Assert.AreEqual(500m, dexUserWallet.GetLastSyncBlock().Balances["tether/TRX"].ToBalanceDecimal(), "Ext token amount error");

            // put external token to dex wallet
            var putokret = await dexUserWallet.DexPutTokenAsync(dexbrk1.AccountID, "tether/TRX", 500m);
            Assert.IsTrue(putokret.Successful(), "Put token error");
            Assert.IsTrue(dexUserWallet.WaitForWorkflow(putokret.TxHash));

            var brk1lstret2 = await client.GetLastBlockAsync(dexbrk1.AccountID);
            Assert.IsTrue(brk1lstret2.Successful());
            var brk1lastblk = brk1lstret2.GetBlock() as TransactionBlock;
            if (networkId == "xunit")
            {
                Assert.AreEqual(1000m, brk1lastblk.Balances["tether/TRX"].ToBalanceDecimal(), "brk1 ext tok balance error");
            }

            // withdraw token to external blockchain
            var wdwret = await dexUserWallet.DexWithdrawTokenAsync(dexbrk1.AccountID, "Txxxxxxxxx", 1000m);
            Assert.IsTrue(wdwret.Successful(), "Error withdraw");
            Assert.IsTrue(dexUserWallet.WaitForWorkflow(wdwret.TxHash));

            var brk1lstret3 = await client.GetLastBlockAsync(dexbrk1.AccountID);
            Assert.IsTrue(brk1lstret3.Successful());
            var brk1lastblk3 = brk1lstret3.GetBlock() as TokenBurnBlock;
            if (networkId == "xunit")
                Assert.AreEqual(0m, brk1lastblk3.Balances["tether/TRX"].ToBalanceDecimal(), "brk1 ext tok burn error");


        }
    }
}
