using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Exchange;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.API
{
    public class LyraRestClient : INodeAPI, INodeTransactionAPI, INodeDexAPI
    {
        private string _appName;
        private string _appVersion;
        private string _url;
        private HttpClient _client;
        public LyraRestClient(string platform, string appName, string appVersion, string url)
        {
            _url = url;
            _appName = appName;
            _appVersion = appVersion;

            if(platform == "iOS")
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) =>
                {
                    if (certificate.Issuer.Equals("CN=localhost"))
                        return true;
                    return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
                };
            }

            var httpClientHandler = new HttpClientHandler();
            // Return `true` to allow certificates that are untrusted/invalid

            if(platform == "Android" || platform == "Windows" || platform == "Win32NT")
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (a, b, c, d) => true;                    
            }

            System.Net.ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => true;
            httpClientHandler.ServerCertificateCustomValidationCallback = (a, b, c, d) => true;

            _client = new HttpClient(httpClientHandler);
            _client.BaseAddress = new Uri(url);
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
#if DEBUG
            _client.Timeout = new TimeSpan(1, 0, 0);
#endif
        }

        public static async Task<LyraRestClient> CreateAsync(string networkId, string platform, string appName, string appVersion, string apiUrl = null)
        {
            var url = apiUrl == null ? LyraGlobal.SelectNode(networkId) + "LyraNode/" : apiUrl;
            var restClient = new LyraRestClient(platform, appName, appVersion, url);
            if (!await restClient.CheckApiVersion().ConfigureAwait(false))
                throw new Exception("Unable to use API. Must upgrade your App.");
            else
                return restClient;
        }

        private async Task<AuthorizationAPIResult> PostBlock(string action, Block block)
        {
            return await PostBlock<AuthorizationAPIResult>(action, block).ConfigureAwait(false);
        }

        private async Task<T> PostBlock<T>(string action, object obj)
        {
            HttpResponseMessage response = await _client.PostAsJsonAsync(
                    action, obj).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<T>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        private async Task<T> Get<T>(string action, Dictionary<string, string> args)
        {
            var url = $"{action}/?" + args?.Aggregate(new StringBuilder(),
                          (sb, kvp) => sb.AppendFormat("{0}{1}={2}",
                                       sb.Length > 0 ? "&" : "", kvp.Key, kvp.Value),
                          sb => sb.ToString());
            HttpResponseMessage response = await _client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<T>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        private async Task<bool> CheckApiVersion()
        {
            var ret = await GetVersion(LyraGlobal.ProtocolVersion, _appName, _appVersion);
            if (ret.MustUpgradeToConnect)
                return false;
            else
                return true;
        }

        public Task<BillBoard> GetBillBoardAsync()
        {
            return Get<BillBoard>("GetBillboard", null);
        }

        public Task<List<TransStats>> GetTransStatsAsync()
        {
            return Get<List<TransStats>>("GetTransStats", null);
        }

        public Task<string> GetDbStats()
        {
            return Get<string>("GetDbStats", null);
        }

        public async Task<GetSyncStateAPIResult> GetSyncState()
        {
            HttpResponseMessage response = await _client.GetAsync("GetSyncState");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<GetSyncStateAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<CreateBlockUIdAPIResult> CreateBlockUId(string AccountId, string Signature, string blockHash)
        {
            var args = new Dictionary<string, string>();

            args.Add("AccountId", AccountId);
            args.Add("Signature", Signature);
            args.Add("blockHash", blockHash);

            return await Get<CreateBlockUIdAPIResult>("CreateBlockUId", args);
        }

        public async Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetVersion/?apiVersion={apiVersion}&appName={appName}&appVersion={appVersion}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<GetVersionAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock block)
        {
            return await PostBlock("CreateToken", block);
        }

        public async Task<AccountHeightAPIResult> GetAccountHeight(string AccountId, string Signature)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetAccountHeight/?AccountId={AccountId}&Signature={Signature}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<AccountHeightAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            throw new NotImplementedException();
        }

        public async Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetBlockByHash/?AccountId={AccountId}&Signature={Signature}&Hash={Hash}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<BlockAPIResult> GetBlockByIndex(string AccountId, long Index, string Signature)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetBlockByIndex/?AccountId={AccountId}&Signature={Signature}&Index={Index}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<BlockAPIResult> GetLastServiceBlock(string AccountId, string Signature)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetLastServiceBlock/?AccountId={AccountId}&Signature={Signature}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<BlockAPIResult> GetLastConsolidationBlock(string AccountId, string Signature)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetLastConsolidationBlock/?AccountId={AccountId}&Signature={Signature}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<MultiBlockAPIResult> GetBlocksByConsolidation(string AccountId, string Signature, string consolidationHash)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetBlocksByConsolidation/?AccountId={AccountId}&Signature={Signature}&consolidationHash={consolidationHash}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<MultiBlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<MultiBlockAPIResult> GetConsolidationBlocks(string AccountId, string Signature, long startHeight)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetConsolidationBlocks/?AccountId={AccountId}&Signature={Signature}&startHeight={startHeight}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<MultiBlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<GetListStringAPIResult> GetUnConsolidatedBlocks(string AccountId, string Signature)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetUnConsolidatedBlocks/?AccountId={AccountId}&Signature={Signature}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<GetListStringAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetNonFungibleTokens/?AccountId={AccountId}&Signature={Signature}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<NonFungibleListAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<AccountHeightAPIResult> GetSyncHeight()
        {
            HttpResponseMessage response = await _client.GetAsync("GetSyncHeight");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<AccountHeightAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetTokenGenesisBlock/?AccountId={AccountId}&Signature={Signature}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<GetListStringAPIResult> GetTokenNames(string AccountId, string Signature, string keyword)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetTokenNames/?AccountId={AccountId}&Signature={Signature}&keyword={keyword}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<GetListStringAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            return await PostBlock("ImportAccount", block);
        }

        public Task<TradeAPIResult> LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            throw new NotImplementedException();
        }

        public async Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            HttpResponseMessage response = await _client.GetAsync($"LookForNewTransfer/?AccountId={AccountId}&Signature={Signature}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<NewTransferAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock block)
        {
            return await PostBlock("OpenAccountWithGenesis", block);
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            return await PostBlock("OpenAccountWithImport", block);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock block)
        {
            return await PostBlock("ReceiveTransfer", block);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock block)
        {
            return await PostBlock("ReceiveTransferAndOpenAccount", block);
        }

        public async Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock block)
        {
            return await PostBlock("SendTransfer", block);
        }

        public async Task<AuthorizationAPIResult> SendExchangeTransfer(ExchangingBlock block)
        {
            return await PostBlock("SendExchangeTransfer", block);
        }

        public async Task<CancelKey> SubmitExchangeOrder(TokenTradeOrder order)
        {
            return await PostBlock<CancelKey>("SubmitExchangeOrder", order);
        }

        public async Task<APIResult> RequestMarket(string tokenName)
        {
            var args = new Dictionary<string, string>();
            args.Add("TokenName", tokenName);
            return await Get<APIResult>("RequestMarket", args);
        }

        public async Task<List<ExchangeOrder>> GetOrdersForAccount(string AccountId, string Signature)
        {
            var args = new Dictionary<string, string>();
            args.Add("AccountId", AccountId);
            args.Add("Signature", Signature);
            return await Get<List<ExchangeOrder>>("GetOrdersForAccount", args);
        }

        public async Task<ExchangeAccountAPIResult> CreateExchangeAccount(string AccountId, string Signature)
        {
            var args = new Dictionary<string, string>();
            args.Add("AccountId", AccountId);
            args.Add("Signature", Signature);
            return await Get<ExchangeAccountAPIResult>("CreateExchangeAccount", args);
        }

        public async Task<ExchangeBalanceAPIResult> GetExchangeBalance(string AccountId, string Signature)
        {
            var args = new Dictionary<string, string>();
            args.Add("AccountId", AccountId);
            args.Add("Signature", Signature);
            return await Get<ExchangeBalanceAPIResult>("GetExchangeBalance", args);
        }

        public async Task<APIResult> CancelExchangeOrder(string AccountId, string Signature, string cancelKey)
        {
            var args = new Dictionary<string, string>();
            args.Add("AccountId", AccountId);
            args.Add("Signature", Signature);
            args.Add("cancelKey", cancelKey);
            return await Get<APIResult>("CancelExchangeOrder", args);
        }

        public Task<ExchangeAccountAPIResult> CloseExchangeAccount(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }
    }
}
