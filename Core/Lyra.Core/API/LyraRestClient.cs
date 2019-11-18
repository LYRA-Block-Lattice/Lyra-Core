using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.API
{
    public class LyraRestClient : INodeAPI
    {
        private string _url;
        private HttpClient _client;
        public LyraRestClient(string url)
        {
            _url = url;

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

        public static LyraRestClient Create(string networkId)
        {
            var url = LyraRpcClient.SelectNode(networkId).Item2;
            return new LyraRestClient(url);
        }

        private async Task<AuthorizationAPIResult> PostBlock(string action, Block block)
        {
            HttpResponseMessage response = await _client.PostAsJsonAsync(
                    action, block);
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<AuthorizationAPIResult>();
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
    }
}
