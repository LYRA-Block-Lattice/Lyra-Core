using Grpc.Net.Client;
using Lyra.Client.Lib;
using Lyra.Core.LiteDB;
using Lyra.Core.Protos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AutoSender
{
    public class WalletUitl
    {
        Wallet wallet;
        public async Task OpenWalletFile(string netName, string dataPath, string walletName)
        {
            if (wallet != null)
                throw new Exception("Wallet opening");

            wallet = new Wallet(new LiteAccountDatabase(), netName);
            wallet.AccountName = walletName;
            await Task.Run(() => wallet.OpenAccount(dataPath, wallet.AccountName));
            var AccountID = wallet.AccountId;
            var PrivateKey = wallet.PrivateKey;
        }

        public async Task<Dictionary<string, Decimal>> RefreshBalance(string webApiUrl)
        {
            var node_address = webApiUrl;
            var channel = GrpcChannel.ForAddress("https://localhost:5001");
            var rpcClient = new LyraRpcClient(channel);

            var result = await wallet.Sync(rpcClient);
            if (result == APIResultCodes.Success)
            {
                return wallet.GetLatestBlock()?.Balances;
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
            if (result != APIResultCodes.Success)
            {
                throw new Exception(result.ToString());
            }
        }
    }
}
