using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
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
    public class LyraRestClient : INodeAPI
    {
        private string _appName;
        private string _appVersion;
        private string _url;
        private HttpClient _client;
        public LyraRestClient(string appName, string appVersion, string url)
        {
            _url = url;
            _appName = appName;
            _appVersion = appVersion;

            var httpClientHandler = new HttpClientHandler();
            // Return `true` to allow certificates that are untrusted/invalid
            httpClientHandler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            _client = new HttpClient(httpClientHandler);
            _client.BaseAddress = new Uri(url);
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public static async Task<LyraRestClient> CreateAsync(string networkId, string appName, string appVersion)
        {
            var url = LyraGlobal.SelectNode(networkId).restUrl + "LyraNode/";
            var restClient = new LyraRestClient(appName, appVersion, url);
            if (!await restClient.CheckApiVersion())
                throw new Exception("Unable to use API. Must upgrade your App.");
            else
                return restClient;
        }

        private async Task<AuthorizationAPIResult> PostBlock(string action, Block block)
        {
            return await PostBlock<AuthorizationAPIResult>(action, block);
        }

        private async Task<T> PostBlock<T>(string action, object obj)
        {
            HttpResponseMessage response = await _client.PostAsJsonAsync(
                    action, obj);
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
            var ret = await GetVersion(LyraGlobal.APIVERSION, _appName, _appVersion);
            if (ret.MustUpgradeToConnect)
                return false;
            else
                return true;
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

        Task<AuthorizationAPIResult> INodeAPI.CancelTradeOrder(CancelTradeOrderBlock block)
        {
            throw new NotImplementedException();
        }

        async Task<AuthorizationAPIResult> INodeAPI.CreateToken(TokenGenesisBlock block)
        {
            return await PostBlock("CreateToken", block);
        }

        Task<AuthorizationAPIResult> INodeAPI.ExecuteTradeOrder(ExecuteTradeOrderBlock block)
        {
            throw new NotImplementedException();
        }

        async Task<AccountHeightAPIResult> INodeAPI.GetAccountHeight(string AccountId, string Signature)
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

        Task<ActiveTradeOrdersAPIResult> INodeAPI.GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            throw new NotImplementedException();
        }

        async Task<BlockAPIResult> INodeAPI.GetBlockByHash(string AccountId, string Hash, string Signature)
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

        async Task<BlockAPIResult> INodeAPI.GetBlockByIndex(string AccountId, int Index, string Signature)
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

        async Task<BlockAPIResult> INodeAPI.GetLastServiceBlock(string AccountId, string Signature)
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

        async Task<NonFungibleListAPIResult> INodeAPI.GetNonFungibleTokens(string AccountId, string Signature)
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

        async Task<AccountHeightAPIResult> INodeAPI.GetSyncHeight()
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

        async Task<BlockAPIResult> INodeAPI.GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
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

        async Task<GetTokenNamesAPIResult> INodeAPI.GetTokenNames(string AccountId, string Signature, string keyword)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetTokenNames/?AccountId={AccountId}&Signature={Signature}&keyword={keyword}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<GetTokenNamesAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        async Task<AuthorizationAPIResult> INodeAPI.ImportAccount(ImportAccountBlock block)
        {
            return await PostBlock("ImportAccount", block);
        }

        Task<TradeAPIResult> INodeAPI.LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            throw new NotImplementedException();
        }

        async Task<NewTransferAPIResult> INodeAPI.LookForNewTransfer(string AccountId, string Signature)
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

        async Task<AuthorizationAPIResult> INodeAPI.OpenAccountWithGenesis(LyraTokenGenesisBlock block)
        {
            return await PostBlock("OpenAccountWithGenesis", block);
        }

        async Task<AuthorizationAPIResult> INodeAPI.OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            return await PostBlock("OpenAccountWithImport", block);
        }

        async Task<AuthorizationAPIResult> INodeAPI.ReceiveTransfer(ReceiveTransferBlock block)
        {
            return await PostBlock("ReceiveTransfer", block);
        }

        async Task<AuthorizationAPIResult> INodeAPI.ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock block)
        {
            return await PostBlock("ReceiveTransferAndOpenAccount", block);
        }

        async Task<AuthorizationAPIResult> INodeAPI.SendTransfer(SendTransferBlock block)
        {
            return await PostBlock("SendTransfer", block);
        }

        Task<AuthorizationAPIResult> INodeAPI.Trade(TradeBlock block)
        {
            throw new NotImplementedException();
        }

        Task<TradeOrderAuthorizationAPIResult> INodeAPI.TradeOrder(TradeOrderBlock block)
        {
            throw new NotImplementedException();
        }

        async Task<CancelKey> INodeAPI.SubmitExchangeOrder(TokenTradeOrder order)
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
    }
}
