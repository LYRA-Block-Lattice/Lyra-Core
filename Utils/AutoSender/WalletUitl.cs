using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoSender
{
    public class WalletUitl
    {
        Wallet wallet;
        public void OpenWalletFile(string netName, string dataPath, string walletName)
        {
            if (wallet != null)
                throw new Exception("Wallet opening");

            wallet = new Wallet(new LiteAccountDatabase(), netName);
            wallet.AccountName = walletName;
            wallet.OpenAccount(dataPath, wallet.AccountName);
            var AccountID = wallet.AccountId;
            var PrivateKey = wallet.PrivateKey;
        }

        public async Task<Dictionary<string, Decimal>> RefreshBalance(string networkId)
        {
            var rpcClient = await LyraRestClient.CreateAsync(networkId, "Windows", "AutoSender", "0.1");

            var result = await wallet.Sync(rpcClient);
            if (result == Lyra.Core.Blocks.APIResultCodes.Success)
            {
                return wallet.GetLatestBlock()?.Balances.ToDictionary(p => p.Key, p => p.Value.ToBalanceDecimal());
            }
            else
            {
                throw new Exception(result.ToString());
            }
        }

        public async Task Transfer(string tokenName, string targetAccount, decimal amount)
        {
            //// refresh balance before send. other wise Null Ex
            //await RefreshBalance();
            //if (App.Container.Balances[tokenName] < amount)
            //{
            //    throw new Exception("Not enough funds for " + tokenName);
            //}

            var result = await wallet.Send(amount, targetAccount, tokenName);
            if (result.ResultCode != APIResultCodes.Success)
            {
                throw new Exception(result.ToString());
            }
        }
    }
}
