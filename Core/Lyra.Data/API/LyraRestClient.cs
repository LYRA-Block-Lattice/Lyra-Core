using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Blocks;
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
        private readonly string _appName;
        private readonly string _appVersion;
        private readonly string _url;

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

            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(_url),
                //_client.DefaultRequestHeaders.Accept.Clear();
                //_client.DefaultRequestHeaders.Accept.Add(
                //    new MediaTypeWithQualityHeaderValue("application/json"));
                //#if DEBUG
                //            _client.Timeout = new TimeSpan(0, 0, 30);
                //#else
                Timeout = new TimeSpan(0, 0, 15)
            };
            //#endif
            return client;
        }

        public static LyraRestClient Create(string networkId, string platform, string appName, string appVersion, string apiUrl = null)
        {
            var url = apiUrl == null ? LyraGlobal.SelectNode(networkId) + "Node/" : apiUrl;
            var uri = new Uri(url);
            var restClient = new LyraRestClient(platform, appName, appVersion, url)
            {
                Host = uri.Host
            };
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

        private async Task<AuthorizationAPIResult> PostBlockAsync(string action, Block block)
        {
            return await PostBlockAsync<AuthorizationAPIResult>(action, block).ConfigureAwait(false);
        }

        private async Task<T> PostBlockAsync<T>(string action, object obj)
        {
            using var client = CreateClient();
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

        private async Task<T> GetAsync<T>(string action, Dictionary<string, string> args)
        {
            var url = $"{action}/?" + args?.Aggregate(new StringBuilder(),
                          (sb, kvp) => sb.AppendFormat("{0}{1}={2}",
                                       sb.Length > 0 ? "&" : "", kvp.Key, kvp.Value),
                          sb => sb.ToString());

            using var client = CreateClient();
            HttpResponseMessage response = await client.GetAsync(url, _cancel.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<T>();
                return result;
            }
            else
                throw new Exception($"Web Api Failed for {_url}");
        }

        private async Task<BlockAPIResult> GetBlockByUrlAsync(string url)
        {
            using var client = CreateClient();
            HttpResponseMessage response = await client.GetAsync(url, _cancel.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<BlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        private async Task<MultiBlockAPIResult> GetMultiBlockByUrlAsync(string url)
        {
            using var client = CreateClient();
            HttpResponseMessage response = await client.GetAsync(url, _cancel.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<MultiBlockAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        private async Task<bool> CheckApiVersionAsync()
        {
            var ret = await GetVersionAsync(LyraGlobal.ProtocolVersion, _appName, _appVersion);
            if (ret.MustUpgradeToConnect)
                return false;
            else
                return true;
        }

        public Task<BillBoard> GetBillBoardAsync()
        {
            return GetAsync<BillBoard>("GetBillboard", null);
        }

        public Task<List<TransStats>> GetTransStatsAsync()
        {
            return GetAsync<List<TransStats>>("GetTransStats", null);
        }

        public Task<string> GetDbStatsAsync()
        {
            return GetAsync<string>("GetDbStats", null);
        }

        public async Task<GetSyncStateAPIResult> GetSyncStateAsync()
        {
            using var client = CreateClient();
            HttpResponseMessage response = await client.GetAsync("GetSyncState", _cancel.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<GetSyncStateAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<GetVersionAPIResult> GetVersionAsync(int apiVersion, string appName, string appVersion)
        {
            using var client = CreateClient();
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

        public async Task<AuthorizationAPIResult> CreateTokenAsync(TokenGenesisBlock block)
        {
            return await PostBlockAsync("CreateToken", block);
        }

        public async Task<AccountHeightAPIResult> GetAccountHeightAsync(string AccountId)
        {
            using var client = CreateClient();
            HttpResponseMessage response = await client.GetAsync($"GetAccountHeight/?AccountId={AccountId}", _cancel.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<AccountHeightAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        #region All reward trade methods

        public async Task<TradeAPIResult> LookForNewTradeAsync(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            var args = new Dictionary<string, string>
            {
                { "AccountId", AccountId },
                { "BuyTokenCode", BuyTokenCode },
                { "SellTokenCode", SellTokenCode },
                { "Signature", Signature }
            };
            return await GetAsync<TradeAPIResult>("LookForNewTrade", args);
        }

        public async Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrdersAsync(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            var args = new Dictionary<string, string>
            {
                { "AccountId", AccountId },
                { "SellToken", SellToken },
                { "BuyToken", BuyToken },
                { "OrderType", OrderType.ToString() },
                { "Signature", Signature }
            };
            return await GetAsync<ActiveTradeOrdersAPIResult>("GetActiveTradeOrders", args);
        }

        public async Task<TradeOrderAuthorizationAPIResult> TradeOrderAsync(TradeOrderBlock tradeOrderBlock)
        {
            //return await PostBlock("TradeOrder", tradeOrderBlock);
            using var client = CreateClient();
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

        public async Task<AuthorizationAPIResult> TradeAsync(TradeBlock block)
        {
            return await PostBlockAsync("Trade", block);
        }

        public async Task<AuthorizationAPIResult> ExecuteTradeOrderAsync(ExecuteTradeOrderBlock block)
        {
            return await PostBlockAsync("ExecuteTradeOrder", block);
        }

        public async Task<AuthorizationAPIResult> CancelTradeOrderAsync(CancelTradeOrderBlock block)
        {
            return await PostBlockAsync("CancelTradeOrder", block);
        }

        #endregion

        public async Task<BlockAPIResult> GetBlockByHashAsync(string AccountId, string Hash, string Signature)
        {
            return await GetBlockByUrlAsync($"GetBlockByHash/?AccountId={AccountId}&Signature={Signature}&Hash={Hash}");
        }

        public async Task<BlockAPIResult> GetBlockAsync(string Hash)
        {
            return await GetBlockByUrlAsync($"GetBlock/?Hash={Hash}");
        }

        public async Task<BlockAPIResult> GetBlockBySourceHashAsync(string Hash)
        {
            return await GetBlockByUrlAsync($"GetBlockBySourceHash/?Hash={Hash}");
        }

        public async Task<MultiBlockAPIResult> GetBlocksByRelatedTxAsync(string Hash)
        {
            return await GetMultiBlockByUrlAsync($"GetBlocksByRelatedTx/?Hash={Hash}");
        }

        public async Task<BlockAPIResult> GetBlockByIndexAsync(string AccountId, long Index)
        {
            return await GetBlockByUrlAsync($"GetBlockByIndex/?AccountId={AccountId}&Index={Index}");
        }

        public async Task<BlockAPIResult> GetLastBlockAsync(string AccountId)
        {
            return await GetBlockByUrlAsync($"GetLastBlock/?AccountId={AccountId}");
        }

        public async Task<BlockAPIResult> GetServiceBlockByIndexAsync(string blockType, long Index)
        {
            return await GetBlockByUrlAsync($"GetServiceBlockByIndex/?blockType={blockType}&Index={Index}");
        }

        public async Task<BlockAPIResult> GetLastServiceBlockAsync()
        {
            return await GetBlockByUrlAsync($"GetLastServiceBlock");
        }

        public async Task<BlockAPIResult> GetLastConsolidationBlockAsync()
        {
            return await GetBlockByUrlAsync($"GetLastConsolidationBlock");
        }

        public async Task<MultiBlockAPIResult> GetBlocksByConsolidationAsync(string AccountId, string Signature, string consolidationHash)
        {
            return await GetMultiBlockByUrlAsync($"GetBlocksByConsolidation/?AccountId={AccountId}&Signature={Signature}&consolidationHash={consolidationHash}");
        }

        public async Task<MultiBlockAPIResult> GetBlockByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            return await GetBlocksByTimeRangeAsync(startTime.Ticks, endTime.Ticks);
        }

        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            return await GetBlockHashesByTimeRangeAsync(startTime.Ticks, endTime.Ticks);
        }

        public async Task<MultiBlockAPIResult> GetBlocksByTimeRangeAsync(long startTimeTicks, long endTimeTicks)
        {
            return await GetMultiBlockByUrlAsync($"GetBlockByTimeRange2/?startTimeTicks={startTimeTicks}&endTimeTicks={endTimeTicks}");
        }

        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRangeAsync(long startTimeTicks, long endTimeTicks)
        {
            using var client = CreateClient();
            HttpResponseMessage response = await client.GetAsync($"GetBlockHashesByTimeRange2/?startTimeTicks={startTimeTicks}&endTimeTicks={endTimeTicks}", _cancel.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<GetListStringAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<MultiBlockAPIResult> GetConsolidationBlocksAsync(string AccountId, string Signature, long startHeight, int count)
        {
            return await GetMultiBlockByUrlAsync($"GetConsolidationBlocks/?AccountId={AccountId}&Signature={Signature}&startHeight={startHeight}&count={count}");
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

        public async Task<NonFungibleListAPIResult> GetNonFungibleTokensAsync(string AccountId, string Signature)
        {
            using var client = CreateClient();
            HttpResponseMessage response = await client.GetAsync($"GetNonFungibleTokens/?AccountId={AccountId}&Signature={Signature}", _cancel.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<NonFungibleListAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<AccountHeightAPIResult> GetSyncHeightAsync()
        {
            using var client = CreateClient();
            HttpResponseMessage response = await client.GetAsync("GetSyncHeight", _cancel.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<AccountHeightAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<BlockAPIResult> GetTokenGenesisBlockAsync(string AccountId, string TokenTicker, string Signature)
        {
            return await GetBlockByUrlAsync($"GetTokenGenesisBlock/?AccountId={AccountId}&TokenTicker={TokenTicker}&Signature={Signature}");
        }

        public async Task<GetListStringAPIResult> GetTokenNamesAsync(string AccountId, string Signature, string keyword)
        {
            using var client = CreateClient();
            HttpResponseMessage response = await client.GetAsync($"GetTokenNames/?AccountId={AccountId}&Signature={Signature}&keyword={keyword}", _cancel.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<GetListStringAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<AuthorizationAPIResult> ImportAccountAsync(ImportAccountBlock block)
        {
            return await PostBlockAsync("ImportAccount", block);
        }

        [Obsolete]
        public async Task<NewTransferAPIResult> LookForNewTransferAsync(string AccountId, string Signature)
        {
            using HttpClient client = CreateClient();
            HttpResponseMessage response = await client.GetAsync($"LookForNewTransfer/?AccountId={AccountId}&Signature={Signature}", _cancel.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<NewTransferAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<NewTransferAPIResult2> LookForNewTransfer2Async(string AccountId, string Signature)
        {
            using var client = CreateClient();
            HttpResponseMessage response = await client.GetAsync($"LookForNewTransfer2/?AccountId={AccountId}&Signature={Signature}", _cancel.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<NewTransferAPIResult2>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<NewFeesAPIResult> LookForNewFeesAsync(string AccountId, string Signature)
        {
            using var client = CreateClient();
            HttpResponseMessage response = await client.GetAsync($"LookForNewFees/?AccountId={AccountId}&Signature={Signature}", _cancel.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<NewFeesAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithGenesisAsync(LyraTokenGenesisBlock block)
        {
            return await PostBlockAsync("OpenAccountWithGenesis", block);
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithImportAsync(OpenAccountWithImportBlock block)
        {
            return await PostBlockAsync("OpenAccountWithImport", block);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransferAsync(ReceiveTransferBlock block)
        {
            return await PostBlockAsync("ReceiveTransfer", block);
        }

        public async Task<AuthorizationAPIResult> ReceiveFeeAsync(ReceiveNodeProfitBlock block)
        {
            return await PostBlockAsync("ReceiveFee", block);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccountAsync(OpenWithReceiveTransferBlock block)
        {
            return await PostBlockAsync("ReceiveTransferAndOpenAccount", block);
        }

        public async Task<AuthorizationAPIResult> SendTransferAsync(SendTransferBlock block)
        {
            return await PostBlockAsync("SendTransfer", block);
        }

        public async Task<BlockAPIResult> GetServiceGenesisBlockAsync()
        {
            return await GetBlockByUrlAsync($"GetServiceGenesisBlock");
        }

        public async Task<BlockAPIResult> GetLyraTokenGenesisBlockAsync()
        {
            return await GetBlockByUrlAsync($"GetLyraTokenGenesisBlock");
        }

        public async Task<List<Voter>> GetVotersAsync(VoteQueryModel model)
        {
            using var client = CreateClient();
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

        public async Task<List<Vote>> FindVotesAsync(VoteQueryModel model)
        {
            using var client = CreateClient();
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

        public async Task<FeeStats> GetFeeStatsAsync()
        {
            return await GetAsync<FeeStats>("GetFeeStats", null);
        }

        public List<Voter> GetVoters(VoteQueryModel model)
        {
            throw new NotImplementedException();
        }

        public List<Vote> FindVotes(VoteQueryModel model)
        {
            throw new NotImplementedException();
        }

        public FeeStats GetFeeStats()
        {
            throw new NotImplementedException();
        }

        public async Task<MultiBlockAPIResult> GetBlocksByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            var args = new Dictionary<string, string>
            {
                { "startTime", startTime.ToLongTimeString() },
                { "endTime", endTime.ToLongTimeString() }
            };

            return await GetAsync<MultiBlockAPIResult>("GetBlocksByTimeRange", args);
        }

        public async Task<TransactionsAPIResult> SearchTransactionsAsync(string accountId, long startTimeTicks, long endTimeTicks, int count)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
                { "count", count.ToString() },
                { "startTimeTicks", startTimeTicks.ToString() },
                { "endTimeTicks", endTimeTicks.ToString() }
            };

            return await GetAsync<TransactionsAPIResult>("SearchTransactions", args);
        }

        public async Task<PoolInfoAPIResult> GetPoolAsync(string token0, string token1)
        {
            var args = new Dictionary<string, string>
            {
                { "token0", token0 },
                { "token1", token1 }
            };

            return await GetAsync<PoolInfoAPIResult>("GetPool", args);
        }

        public async Task<MultiBlockAPIResult> GetAllBrokerAccountsForOwnerAsync(string ownerAccount)
        {
            var args = new Dictionary<string, string>
            {
                { "ownerAccount", ownerAccount },
            };

            return await GetAsync<MultiBlockAPIResult>("GetAllBrokerAccountsForOwner", args);
        }

        public async Task<List<Profiting>> FindAllProfitingAccountsAsync(DateTime begin, DateTime end)
        {
            var args = new Dictionary<string, string>
            {
                { "timeBeginTicks", begin.Ticks.ToString() },
                { "timeEndTicks", end.Ticks.ToString() }
            };

            return await GetAsync<List<Profiting>>("FindAllProfitingAccounts", args);
        }

        public async Task<ProfitingGenesis> FindProfitingAccountsByNameAsync(string Name)
        {
            var args = new Dictionary<string, string>
            {
                { "name", Name }
            };

            return await GetAsync<ProfitingGenesis>("FindProfitingAccountsByName", args);
        }

        public async Task<List<Staker>> FindAllStakingsAsync(string pftid, DateTime timeBefore)
        {
            var args = new Dictionary<string, string>
            {
                { "pftid", pftid },
                { "timeBeforeTicks", timeBefore.Ticks.ToString() }
            };

            return await GetAsync<List<Staker>>("FindAllStakings", args);
        }

        List<Staker> INodeAPI.FindAllStakings(string pftid, DateTime timeBefore)
        {
            throw new NotImplementedException();
        }

        public async Task<ProfitingStats> GetAccountStatsAsync(string accountId, DateTime begin, DateTime end)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
                { "timeBeginTicks", begin.Ticks.ToString() },
                { "timeEndTicks", end.Ticks.ToString() }
            };

            return await GetAsync<ProfitingStats>("GetAccountStats", args);
        }

        public async Task<ProfitingStats> GetBenefitStatsAsync(string pftid, string stkid, DateTime begin, DateTime end)
        {
            var args = new Dictionary<string, string>
            {
                { "pftid", pftid },
                { "stkid", stkid },
                { "timeBeginTicks", begin.Ticks.ToString() },
                { "timeEndTicks", end.Ticks.ToString() }
            };

            return await GetAsync<ProfitingStats>("GetBenefitStats", args);
        }

        public async Task<PendingStats> GetPendingStatsAsync(string accountId)
        {
            var args = new Dictionary<string, string>
            {

            };

            return await GetAsync<PendingStats>("GetPendingStats", args);
        }

        ProfitingGenesis INodeAPI.FindProfitingAccountsByNameAsync(string Name)
        {
            throw new NotImplementedException();
        }

        public async Task<List<TransactionBlock>> GetAllDexWalletsAsync()
        {
            var args = new Dictionary<string, string>
            {

            };

            return await GetAsync<List<TransactionBlock>>("GetAllDexWallets", args);
        }
        public async Task<DexWalletGenesis> FindDexWalletAsync(string owner, string symbol, string provider)
        {
            var args = new Dictionary<string, string>
            {
                { "owner", owner },
                { "symbol", symbol },
                { "provider", provider }
            };

            return await GetAsync<DexWalletGenesis>("FindDexWallet", args);
        }
    }
}
