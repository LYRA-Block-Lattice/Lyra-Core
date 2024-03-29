﻿using Converto;
using DexServer.Ext;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_AcademyClient
    {
        private string networkId = TestConfig.networkId;

        ILyraAPI client;

        [TestMethod]
        public async Task TestVerifyEmailAsync()
        {
            var wallet = await GetGenesisWalletAsync();

            client = LyraRestClient.Create(networkId, "windows", "unit test", "1.0");
            var lsb = await client.GetLastServiceBlockAsync();
            Assert.IsTrue(lsb.Successful());

            var acac = new AcademyClient(networkId);
            var email = "rcvbuf@gmail.com";
            var input = $"{wallet.AccountId}:{email}:{lsb.GetBlock().Hash}";
            var signatures = Signatures.GetSignature(wallet.PrivateKey, input, wallet.AccountId);
            var retstr = await acac.VerifyEmailAsync(wallet.AccountId, email, signatures);
            dynamic ret = JObject.Parse(retstr);
            Assert.IsTrue(ret.msg == "success", $"Can't verify email: {ret.msg}");

            var vretstr = await acac.GetCodeForEmailAsync(wallet.AccountId, email, signatures);
            dynamic vret = JObject.Parse(vretstr);
            Console.WriteLine($"Email verify code is {vretstr}");
            Assert.IsTrue(vret.code > 0 && vret.msg == "success", $"Can't get email verify code: {vret}");
        }

        [TestMethod]
        public async Task TestGetQuestAsync()
        {
            var AccountId = "LKBW8Yc6T49ruZnm2NCuUKVqVqXTVbsRHXUxcNjpPjGGTM75QrhANue9t6ujERSG6cF1z6TykrQtq6WUPZ5y6NQaED2GgH";
            var acac = new AcademyClient("testnet");
            var ret = await acac.GetAsync<string>("GetQuestState", new Dictionary<string, string> { { "accountId", AccountId } });

            dynamic qs = JObject.Parse(ret);
            Assert.IsTrue(qs.msg == "success" && qs.allDone.ToObject<bool>(), $"ret is {ret}");
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
    }
}
