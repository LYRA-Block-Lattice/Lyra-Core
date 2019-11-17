using Grpc.Net.Client;
using Lyra.Client.Lib;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.LiteDB;
using Lyra.Core.Protos;
using LyraWallet.Services;
using LyraWallet.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace LyraWallet.Models
{
    public class WalletContainer : BaseViewModel
    {
        private string dataStoragePath;
        private string walletFn;

        private string currentNetwork;
        private string accountID;
        private string privateKey;

        private Wallet wallet;
        private Dictionary<string, Decimal> balances;
        private List<string> tokenList;

        // working status
        private string busyMessage;

        public string DataStoragePath { get => dataStoragePath; set => SetProperty(ref dataStoragePath, value); }
        public string WalletFn { get => walletFn; set => SetProperty(ref walletFn, value); }
        public string CurrentNetwork { get => currentNetwork; set => SetProperty(ref currentNetwork, value); }
        public string AccountID { get => accountID; set => SetProperty(ref accountID, value); }
        public string PrivateKey { get => privateKey; set => SetProperty(ref privateKey, value); }
        public Dictionary<string, decimal> Balances { get => balances; set {
                var sorted = value?.OrderBy(a => a.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                SetProperty(ref balances, sorted); }
        }
        public List<string> TokenList { get => tokenList; set => SetProperty(ref tokenList, value); }
        public string BusyMessage { get => busyMessage; set => SetProperty(ref busyMessage, value); }

        public async Task OpenWalletFile()
        {
            if (wallet != null)
                throw new Exception("Wallet opening");

            wallet = new Wallet(new LiteAccountDatabase(), CurrentNetwork);
            wallet.AccountName = "My Account";
            await Task.Run(() => wallet.OpenAccount(App.Container.DataStoragePath, wallet.AccountName));

            AccountID = wallet.AccountId;
            PrivateKey = wallet.PrivateKey;
        }

        public async Task CreateNew(string network_id)
        {
            if (wallet != null)
                throw new Exception("Wallet opening");

            var path = DependencyService.Get<IPlatformSvc>().GetStoragePath();
            File.WriteAllText(path + "network.txt", network_id);
            wallet = new Wallet(new LiteAccountDatabase(), network_id)
            {
                AccountName = "My Account"
            };
            await Task.FromResult(wallet.CreateAccount(path, wallet.AccountName, AccountTypes.Standard));
        }

        public async Task CreateByPrivateKey(string network_id, string privatekey)
        {
            if (wallet != null)
                throw new Exception("Wallet opening");

            var path = DependencyService.Get<IPlatformSvc>().GetStoragePath();
            File.WriteAllText(path + "network.txt", network_id);
            wallet = new Wallet(new LiteAccountDatabase(), network_id)
            {
                AccountName = "My Account"
            };

            if (!wallet.ValidatePrivateKey(privatekey))
            {
                wallet = null;
                throw new InvalidDataException("Invalid Private Key");
            }                

            var result = await Task.FromResult(wallet.RestoreAccount(path, privatekey));
            if (!result.Successful())
            {
                wallet = null;
                throw new InvalidDataException("Could not restore account from file: " + result.ResultMessage);
            }
        }

        public async Task GetBalance()
        {
            var latestBlock = await Task.FromResult(wallet.GetLatestBlock());
            App.Container.Balances = latestBlock?.Balances;
            App.Container.TokenList = App.Container.Balances?.Keys.ToList();
        }
        public async Task RefreshBalance(string webApiUrl = null)
        {
            var node_address = SelectNode(wallet.NetworkId);
            if (webApiUrl != null)
                node_address = webApiUrl;

            var channel = GrpcChannel.ForAddress("https://localhost:5001");
            var rpcClient = new LyraRpcClient(channel);

            var result = await wallet.Sync(rpcClient);
            if (result == APIResultCodes.Success)
            {
                App.Container.Balances = wallet.GetLatestBlock()?.Balances;
                App.Container.TokenList = App.Container.Balances?.Keys.ToList();
            }
            else
            {
                throw new Exception(result.ToString());
            }
        }

        public async Task Transfer(string tokenName, string targetAccount, decimal amount)
        {
            // refresh balance before send. other wise Null Ex
            await RefreshBalance();
            if(App.Container.Balances[tokenName] < amount)
            {
                throw new Exception("Not enough funds for " + tokenName);
            }

            var result = await wallet.Send(amount, targetAccount, tokenName);
            if (result != APIResultCodes.Success)
            {
                throw new Exception(result.ToString());
            }
        }

        public async Task CreateToken(string tokenName, string tokenDomain,
            string description, decimal totalSupply, int precision, string ownerName, string ownerAddress)
        {
            var result = await wallet.CreateToken(tokenName, tokenDomain ?? "", description ?? "", Convert.ToSByte(precision), totalSupply,
                true, ownerName ?? "", ownerAddress ?? "", null, Lyra.Core.Blocks.Transactions.ContractTypes.Default, null);
            if (result != APIResultCodes.Success)
            {
                throw new Exception(result.ToString());
            }
        }

        public async Task<List<BlockInfo>> GetBlocks()
        {
            var blocks = new List<BlockInfo>();
            var height = wallet.GetLocalAccountHeight();
            for (int i = height; i > 0; i--)
            {
                var block = await wallet.GetBlockByIndex(i);
                blocks.Add(new BlockInfo()
                {
                    index = block.Index,
                    timeStamp = block.TimeStamp,
                    hash = block.Hash,
                    type = block.BlockType.ToString(),
                    balance = block.Balances.Aggregate(new StringBuilder(),
                          (sb, kvp) => sb.AppendFormat("{0}{1} = {2}",
                                       sb.Length > 0 ? ", " : "", kvp.Key, kvp.Value),
                          sb => sb.ToString())
                });
            }
            return blocks;
        }

        public async Task<List<string>> GetTokens(string keyword)
        {
            var result = await wallet.GetTokenNames(keyword);
            return result;
        }

        public async Task CloseWallet()
        {
            await Task.Run(() => {
                if (wallet != null)
                    wallet.Dispose();
                wallet = null;
            });
        }

        public async Task Remove()
        {
            await Task.Run(() => {
                if (wallet != null)
                    wallet.Dispose();
                wallet = null;

                if (File.Exists(App.Container.WalletFn))
                    File.Delete(App.Container.WalletFn);
            });
        }

        string SelectNode(string network_id)
        {
            switch (network_id)
            {
#if DEBUG
                case "lexdev":
                    return "http://lex.lyratokens.com:5492/api/";
#endif
                case "lexnet":
                    return "http://lex.lyratokens.com:5392/api/";
                case "testnet":
                    return "http://testnet.lyratokens.com/api/";
                case "mainnet":
                    return "http://mainnet.lyratokens.com/api/";
                default:
                    throw new Exception("Unsupported network ID");
            }
        }
    }
}
