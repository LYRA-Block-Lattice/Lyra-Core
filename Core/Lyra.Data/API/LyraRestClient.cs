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
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Core.API
{
    public class LyraRestClient : ILyraAPI
    {
        public string ServerThumbPrint { get; private set; }
        private string _appName;
        private string _appVersion;
        private string _url;

        public string Host { get; private set; }
        private CancellationTokenSource _cancel;
        public LyraRestClient(string platform, string appName, string appVersion, string url)
        {
            _url = url;
            _appName = appName;
            _appVersion = appVersion;

            _cancel = new CancellationTokenSource();

            if(platform == "iOS")
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) =>
                {
                    if (certificate.Issuer.Equals("CN=localhost"))
                        return true;
                    return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
                };
            }         
        }

        private HttpClient CreateClient()
        {
            var handler = new HttpClientHandler();
            try
            {
                handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, error) =>
                {
                    var cert2 = new X509Certificate2(cert.GetRawCertData());
                    ServerThumbPrint = cert2.Thumbprint;
                    return true;
                };
            }
            catch { }

            var client = new HttpClient(handler);
            client.BaseAddress = new Uri(_url);
            //_client.DefaultRequestHeaders.Accept.Clear();
            //_client.DefaultRequestHeaders.Accept.Add(
            //    new MediaTypeWithQualityHeaderValue("application/json"));
            //#if DEBUG
            //            _client.Timeout = new TimeSpan(0, 0, 30);
            //#else
            client.Timeout = new TimeSpan(0, 0, 15);
            //#endif
            return client;
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

        public void Abort()
        {
            _cancel.Cancel();
            _cancel.Dispose();
            _cancel = new CancellationTokenSource();
        }

        private async Task<AuthorizationAPIResult> PostBlock(string action, Block block)
        {
            return await PostBlock<AuthorizationAPIResult>(action, block).ConfigureAwait(false);
        }

        private async Task<T> PostBlock<T>(string action, object obj)
        {
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.PostAsJsonAsync(
                    action, obj, _cancel.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<T>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
        }

        private async Task<T> Get<T>(string action, Dictionary<string, string> args)
        {
            var url = $"{action}/?" + args?.Aggregate(new StringBuilder(),
                          (sb, kvp) => sb.AppendFormat("{0}{1}={2}",
                                       sb.Length > 0 ? "&" : "", kvp.Key, kvp.Value),
                          sb => sb.ToString());

            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync(url, _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<T>();
                    return result;
                }
                else
                    throw new Exception($"Web Api Failed for {_url}");
            }
        }

        private async Task<BlockAPIResult> GetBlockByUrl(string url)
        {
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync(url, _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
        }

        private async Task<MultiBlockAPIResult> GetMultiBlockByUrl(string url)
        {
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync(url, _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<MultiBlockAPIResult>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
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
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync("GetSyncState", _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<GetSyncStateAPIResult>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
        }

        public async Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion)
        {
            using (var client = CreateClient())
            {
                var api_call = $"GetVersion/?apiVersion={apiVersion}&appName={appName}&appVersion={appVersion}";
                HttpResponseMessage response = await client.GetAsync(api_call, _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<GetVersionAPIResult>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
        }

        public async Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock block)
        {
            return await PostBlock("CreateToken", block);
        }

        public async Task<AccountHeightAPIResult> GetAccountHeight(string AccountId)
        {
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync($"GetAccountHeight/?AccountId={AccountId}", _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<AccountHeightAPIResult>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
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
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.PostAsJsonAsync("TradeOrder", tradeOrderBlock).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<TradeOrderAuthorizationAPIResult>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
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
            return await GetBlockByUrl($"GetBlockByHash/?AccountId={AccountId}&Signature={Signature}&Hash={Hash}");
        }

        public async Task<BlockAPIResult> GetBlock(string Hash)
        {
            return await GetBlockByUrl($"GetBlock/?Hash={Hash}");
        }

        public async Task<BlockAPIResult> GetBlockBySourceHash(string Hash)
        {
            return await GetBlockByUrl($"GetBlockBySourceHash/?Hash={Hash}");
        }

        public async Task<BlockAPIResult> GetBlockByIndex(string AccountId, long Index)
        {
            return await GetBlockByUrl($"GetBlockByIndex/?AccountId={AccountId}&Index={Index}");
        }

        public async Task<BlockAPIResult> GetLastBlock(string AccountId)
        {
            return await GetBlockByUrl($"GetLastBlock/?AccountId={AccountId}");
        }

        public async Task<BlockAPIResult> GetServiceBlockByIndex(string blockType, long Index)
        {
            return await GetBlockByUrl($"GetServiceBlockByIndex/?blockType={blockType}&Index={Index}");
        }

        public async Task<BlockAPIResult> GetLastServiceBlock()
        {
            return await GetBlockByUrl($"GetLastServiceBlock");
        }

        public async Task<BlockAPIResult> GetLastConsolidationBlock()
        {
            return await GetBlockByUrl($"GetLastConsolidationBlock");
        }

        public async Task<MultiBlockAPIResult> GetBlocksByConsolidation(string AccountId, string Signature, string consolidationHash)
        {
            return await GetMultiBlockByUrl($"GetBlocksByConsolidation/?AccountId={AccountId}&Signature={Signature}&consolidationHash={consolidationHash}");
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
            return await GetMultiBlockByUrl($"GetBlockByTimeRange2/?startTimeTicks={startTimeTicks}&endTimeTicks={endTimeTicks}");
        }

        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRange(long startTimeTicks, long endTimeTicks)
        {
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync($"GetBlockHashesByTimeRange2/?startTimeTicks={startTimeTicks}&endTimeTicks={endTimeTicks}", _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<GetListStringAPIResult>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
        }

        public async Task<MultiBlockAPIResult> GetConsolidationBlocks(string AccountId, string Signature, long startHeight, int count)
        {
            return await GetMultiBlockByUrl($"GetConsolidationBlocks/?AccountId={AccountId}&Signature={Signature}&startHeight={startHeight}&count={count}");
        }

        //public async Task<GetListStringAPIResult> GetUnConsolidatedBlocks(string AccountId, string Signature)
        //{
        //    HttpResponseMessage response = await _client.GetAsync($"GetUnConsolidatedBlocks/?AccountId={AccountId}&Signature={Signature}", _cancel.Token);
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
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync($"GetNonFungibleTokens/?AccountId={AccountId}&Signature={Signature}", _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<NonFungibleListAPIResult>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
        }

        public async Task<AccountHeightAPIResult> GetSyncHeight()
        {
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync("GetSyncHeight", _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<AccountHeightAPIResult>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
        }

        public async Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            return await GetBlockByUrl($"GetTokenGenesisBlock/?AccountId={AccountId}&TokenTicker={TokenTicker}&Signature={Signature}");
        }

        public async Task<GetListStringAPIResult> GetTokenNames(string AccountId, string Signature, string keyword)
        {
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync($"GetTokenNames/?AccountId={AccountId}&Signature={Signature}&keyword={keyword}", _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<GetListStringAPIResult>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
        }

        public async Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            return await PostBlock("ImportAccount", block);
        }

        public async Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync($"LookForNewTransfer/?AccountId={AccountId}&Signature={Signature}", _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<NewTransferAPIResult>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
        }

        public async Task<NewTransferAPIResult2> LookForNewTransfer2(string AccountId, string Signature)
        {
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync($"LookForNewTransfer2/?AccountId={AccountId}&Signature={Signature}", _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<NewTransferAPIResult2>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
        }

        public async Task<NewFeesAPIResult> LookForNewFees(string AccountId, string Signature)
        {
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.GetAsync($"LookForNewFees/?AccountId={AccountId}&Signature={Signature}", _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<NewFeesAPIResult>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
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

        public async Task<BlockAPIResult> GetServiceGenesisBlock()
        {
            return await GetBlockByUrl($"GetServiceGenesisBlock");
        }

        public async Task<BlockAPIResult> GetLyraTokenGenesisBlock()
        {
            return await GetBlockByUrl($"GetLyraTokenGenesisBlock");
        }

        public async Task<List<Voter>> GetVotersAsync(VoteQueryModel model)
        {
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.PostAsJsonAsync(
                            "GetVoters", model, _cancel.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<List<Voter>>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
        }

        public async Task<List<Vote>> FindVotesAsync(VoteQueryModel model)
        {
            using (var client = CreateClient())
            {
                HttpResponseMessage response = await client.PostAsJsonAsync(
                            "FindVotes", model, _cancel.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<List<Vote>>();
                    return result;
                }
                else
                    throw new Exception("Web Api Failed.");
            }
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

        public async Task<PoolInfoAPIResult> GetPool(string token0, string token1)
        {
            var args = new Dictionary<string, string>();

            args.Add("token0", token0);
            args.Add("token1", token1);

            return await Get<PoolInfoAPIResult>("GetPool", args);
        }
    }
}
