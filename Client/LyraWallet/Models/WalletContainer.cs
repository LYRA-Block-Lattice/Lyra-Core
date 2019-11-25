using Grpc.Net.Client;
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
using Xamarin.Essentials;
using Lyra.Exchange;
using Lyra.Core.Accounts;

namespace LyraWallet.Models
{
    public delegate void NodeNotifyMessage(string action, string catalog, string extInfo);
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

        private INodeAPI _nodeApiClient;
        private LyraRestNotify _notifyApiClient;

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

        private CancellationTokenSource _cancel;
        public event NodeNotifyMessage OnBalanceChanged;
        public event NodeNotifyMessage OnExchangeOrderChanged;
        public async Task OpenWalletFile()
        {
            if (wallet != null)
                throw new Exception("Wallet opening");

            wallet = new Wallet(new LiteAccountDatabase(), CurrentNetwork);
            wallet.AccountName = "My Account";
            wallet.OpenAccount(App.Container.DataStoragePath, wallet.AccountName);

            AccountID = wallet.AccountId;
            PrivateKey = wallet.PrivateKey;

            // setup API clients
            _nodeApiClient = await LyraRestClient.CreateAsync(CurrentNetwork, AppInfo.Name, AppInfo.VersionString);
            _notifyApiClient = new LyraRestNotify(LyraGlobal.SelectNode(CurrentNetwork).restUrl + "LyraNotify/");

            _cancel = new CancellationTokenSource();
            await _notifyApiClient.BeginReceiveNotifyAsync(AccountID, wallet.SignAPICall(), (source, action, catalog, extInfo) => {
                Device.BeginInvokeOnMainThread(() =>
                {
                    switch (source)
                    {
                        case NotifySource.Balance:
                            OnBalanceChanged?.Invoke(action, catalog, extInfo);
                            break;
                        case NotifySource.Dex:
                            OnExchangeOrderChanged?.Invoke(action, catalog, extInfo);
                            break;
                        default:
                            break;
                    }
                });
            }, _cancel.Token);
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
            var result = await wallet.Sync(_nodeApiClient);
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

        public async Task Transfer(string tokenName, string targetAccount, decimal amount, bool ToExchange = false)
        {
            // refresh balance before send. other wise Null Ex
            await RefreshBalance();
            if(App.Container.Balances[tokenName] < amount)
            {
                throw new Exception("Not enough funds for " + tokenName);
            }

            var result = await wallet.Send(amount, targetAccount, tokenName, ToExchange);
            if (result.ResultCode != APIResultCodes.Success)
            {
                throw new Exception(result.ToString());
            }
        }

        public async Task CreateToken(string tokenName, string tokenDomain,
            string description, decimal totalSupply, int precision, string ownerName, string ownerAddress)
        {
            var result = await wallet.CreateToken(tokenName, tokenDomain ?? "", description ?? "", Convert.ToSByte(precision), totalSupply,
                true, ownerName ?? "", ownerAddress ?? "", null, Lyra.Core.Blocks.Transactions.ContractTypes.Default, null);
            if (result.ResultCode != APIResultCodes.Success)
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
                if(_cancel != null)
                    _cancel.Cancel();
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

        public async Task<string> GetExchangeAccountId()
        {
            var result = await _nodeApiClient.CreateExchangeAccount(AccountID, wallet.SignAPICall());
            if (result.ResultCode == APIResultCodes.Success)
                return result.AccountId;
            else
                return null;
        }

        public async Task<CancelKey> SubmitExchangeOrderAsync(TokenTradeOrder order)
        {
            return await _nodeApiClient.SubmitExchangeOrder(order);
        }

        public async Task<APIResult> CancelExchangeOrder(string key)
        {
            return await _nodeApiClient.CancelExchangeOrder(AccountID, wallet.SignAPICall(), key);
        }

        public async Task<APIResult> RequestMarket(string tokenName)
        {
            return await _nodeApiClient.RequestMarket(tokenName);
        }

        public async Task<List<ExchangeOrder>> GetOrdersForAccount(string AccountId)
        {
            return await _nodeApiClient.GetOrdersForAccount(AccountId, wallet.SignAPICall());
        }

        public async Task<Dictionary<string, decimal>> GetExchangeBalance()
        {
            var result = await _nodeApiClient.GetExchangeBalance(AccountID, wallet.SignAPICall());
            return result.Balance;
        }
    }
}
