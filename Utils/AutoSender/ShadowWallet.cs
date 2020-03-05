using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.LiteDB;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AutoSender
{
    class ShadowWallet
    {
        public ShadowWallet()
        {

        }

        public static async Task<Wallet> OpenAsync(string networkId, string walletName)
        {
            var walletStore = new LiteAccountDatabase();
            var tmpWallet = new Wallet(walletStore, networkId);
            string lyrawalletfolder = BaseAccount.GetFullFolderName("Lyra-CLI-" + networkId);
            tmpWallet.OpenAccount(lyrawalletfolder, walletName);

            // create wallet and update balance
            var memStor = new AccountInMemoryStorage();
            var acctWallet = new ExchangeAccountWallet(memStor, networkId);
            acctWallet.AccountName = "tmpAcct";
            await acctWallet.RestoreAccountAsync("", tmpWallet.PrivateKey);
            acctWallet.OpenAccount("", acctWallet.AccountName);

            Console.WriteLine("Sync wallet for " + acctWallet.AccountId);
            var rpcClient = await LyraRestClient.CreateAsync(networkId, Environment.OSVersion.Platform.ToString(), "Lyra Client Cli", "1.0a");
            await acctWallet.Sync(rpcClient);

            return acctWallet;
        }
    }
}
