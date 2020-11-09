using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
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
        public string Host { get; private set; }
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
                try
                {
                    httpClientHandler.ServerCertificateCustomValidationCallback = (a, b, c, d) => true;
                }
                catch { }
            }

            try
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => true;
            }
            catch { }

            try
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (a, b, c, d) => true;
            }
            catch { }            

            _client = new HttpClient(httpClientHandler);
            _client.BaseAddress = new Uri(url);
            //_client.DefaultRequestHeaders.Accept.Clear();
            //_client.DefaultRequestHeaders.Accept.Add(
            //    new MediaTypeWithQualityHeaderValue("application/json"));
//#if DEBUG
//            _client.Timeout = new TimeSpan(0, 0, 30);
//#else
            _client.Timeout = new TimeSpan(0, 0, 5);
//#endif
        }

        public static LyraRestClient Create(string networkId, string platform, string appName, string appVersion, string apiUrl = null)
        {
            var url = apiUrl == null ? LyraGlobal.SelectNode(networkId) + "Node/" : apiUrl;
            var uri = new Uri(url);            
            var restClient = new LyraRestClient(platform, appName, appVersion, url);
            restClient.Host = uri.Host;
            //if (!await restClient.CheckApiVersion().ConfigureAwait(false))
            //    throw new Exception("Unable to use API. Must upgrade your App.");
            //else
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

        public async Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion)
        {
            var api_call = $"GetVersion/?apiVersion={apiVersion}&appName={appName}&appVersion={appVersion}";
            HttpResponseMessage response = await _client.GetAsync(api_call);
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

        public async Task<AccountHeightAPIResult> GetAccountHeight(string AccountId)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetAccountHeight/?AccountId={AccountId}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<AccountHeightAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        #region All reward trade methods

        public async Task<TradeAPIResult> LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            var args = new Dictionary<string, string>();
            args.Add("AccountId", AccountId);
            args.Add("BuyTokenCode", BuyTokenCode);
            args.Add("SellTokenCode", SellTokenCode);
            args.Add("Signature", Signature);
            return await Get<TradeAPIResult>("LookForNewTrade", args);
        }

        public async Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            var args = new Dictionary<string, string>();
            args.Add("AccountId", AccountId);
            args.Add("SellToken", SellToken);
            args.Add("BuyToken", BuyToken);
            args.Add("OrderType", OrderType.ToString());
            args.Add("Signature", Signature);
            return await Get<ActiveTradeOrdersAPIResult>("GetActiveTradeOrders", args);
        }

        public async Task<TradeOrderAuthorizationAPIResult> TradeOrder(TradeOrderBlock tradeOrderBlock)
        {
            //return await PostBlock("TradeOrder", tradeOrderBlock);

            HttpResponseMessage response = await _client.PostAsJsonAsync("TradeOrder", tradeOrderBlock).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<TradeOrderAuthorizationAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<AuthorizationAPIResult> Trade(TradeBlock block)
        {
            return await PostBlock("Trade", block);
        }

        public async Task<AuthorizationAPIResult> ExecuteTradeOrder(ExecuteTradeOrderBlock block)
        {
            return await PostBlock("ExecuteTradeOrder", block);
        }

        public async Task<AuthorizationAPIResult> CancelTradeOrder(CancelTradeOrderBlock block)
        {
            return await PostBlock("CancelTradeOrder", block);
        }

        #endregion

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

        public async Task<BlockAPIResult> GetBlock(string Hash)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetBlock/?Hash={Hash}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<BlockAPIResult> GetBlockByIndex(string AccountId, long Index)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetBlockByIndex/?AccountId={AccountId}&Index={Index}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<BlockAPIResult> GetLastBlock(string AccountId)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetLastBlock/?AccountId={AccountId}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<BlockAPIResult> GetServiceBlockByIndex(string blockType, long Index)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetServiceBlockByIndex/?blockType={blockType}&Index={Index}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<BlockAPIResult> GetLastServiceBlock()
        {
            HttpResponseMessage response = await _client.GetAsync($"GetLastServiceBlock");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<BlockAPIResult> GetLastConsolidationBlock()
        {
            HttpResponseMessage response = await _client.GetAsync($"GetLastConsolidationBlock");
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

        public async Task<MultiBlockAPIResult> GetBlockByTimeRange(DateTime startTime, DateTime endTime)
        {
            return await GetBlocksByTimeRange(startTime.Ticks, endTime.Ticks);
        }

        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRange(DateTime startTime, DateTime endTime)
        {
            return await GetBlockHashesByTimeRange(startTime.Ticks, endTime.Ticks);
        }

        public async Task<MultiBlockAPIResult> GetBlocksByTimeRange(long startTimeTicks, long endTimeTicks)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetBlockByTimeRange2/?startTimeTicks={startTimeTicks}&endTimeTicks={endTimeTicks}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<MultiBlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRange(long startTimeTicks, long endTimeTicks)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetBlockHashesByTimeRange2/?startTimeTicks={startTimeTicks}&endTimeTicks={endTimeTicks}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<GetListStringAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<MultiBlockAPIResult> GetConsolidationBlocks(string AccountId, string Signature, long startHeight, int count)
        {
            HttpResponseMessage response = await _client.GetAsync($"GetConsolidationBlocks/?AccountId={AccountId}&Signature={Signature}&startHeight={startHeight}&count={count}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<MultiBlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        //public async Task<GetListStringAPIResult> GetUnConsolidatedBlocks(string AccountId, string Signature)
        //{
        //    HttpResponseMessage response = await _client.GetAsync($"GetUnConsolidatedBlocks/?AccountId={AccountId}&Signature={Signature}");
        //    if (response.IsSuccessStatusCode)
        //    {
        //        var result = await response.Content.ReadAsAsync<GetListStringAPIResult>();
        //        return result;
        //    }
        //    else
        //        throw new Exception("Web Api Failed.");
        //}

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
            HttpResponseMessage response = await _client.GetAsync($"GetTokenGenesisBlock/?AccountId={AccountId}&TokenTicker={TokenTicker}&Signature={Signature}");
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

        public async Task<NewFeesAPIResult> LookForNewFees(string AccountId, string Signature)
        {
            HttpResponseMessage response = await _client.GetAsync($"LookForNewFees/?AccountId={AccountId}&Signature={Signature}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<NewFeesAPIResult>();
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

        public async Task<AuthorizationAPIResult> ReceiveFee(ReceiveAuthorizerFeeBlock block)
        {
            return await PostBlock("ReceiveFee", block);
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

        public async Task<BlockAPIResult> GetServiceGenesisBlock()
        {
            HttpResponseMessage response = await _client.GetAsync($"GetServiceGenesisBlock");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<BlockAPIResult> GetLyraTokenGenesisBlock()
        {
            HttpResponseMessage response = await _client.GetAsync($"GetLyraTokenGenesisBlock");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<List<Voter>> GetVotersAsync(VoteQueryModel model)
        {
            HttpResponseMessage response = await _client.PostAsJsonAsync(
                    "GetVoters", model).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<List<Voter>>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<List<Vote>> FindVotesAsync(VoteQueryModel model)
        {
            HttpResponseMessage response = await _client.PostAsJsonAsync(
                    "FindVotes", model).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<List<Vote>>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<FeeStats> GetFeeStatsAsync()
        {
            return await Get<FeeStats>("GetFeeStats", null);
        }

        List<Voter> INodeAPI.GetVoters(VoteQueryModel model)
        {
            throw new NotImplementedException();
        }

        List<Vote> INodeAPI.FindVotes(VoteQueryModel model)
        {
            throw new NotImplementedException();
        }

        FeeStats INodeAPI.GetFeeStats()
        {
            throw new NotImplementedException();
        }

        public async Task<MultiBlockAPIResult> GetBlocksByTimeRange(DateTime startTime, DateTime endTime)
        {
            var args = new Dictionary<string, string>();

            args.Add("startTime", startTime.ToLongTimeString());
            args.Add("endTime", endTime.ToLongTimeString());

            return await Get<MultiBlockAPIResult>("GetBlocksByTimeRange", args);
        }

        public async Task<TransactionsAPIResult> SearchTransactions(string accountId, long startTimeTicks, long endTimeTicks, int count)
        {
            var args = new Dictionary<string, string>();

            args.Add("accountId", accountId);
            args.Add("count", count.ToString());
            args.Add("startTimeTicks", startTimeTicks.ToString());
            args.Add("endTimeTicks", endTimeTicks.ToString());

            return await Get<TransactionsAPIResult>("SearchTransactions", args);
        }
    }
}
