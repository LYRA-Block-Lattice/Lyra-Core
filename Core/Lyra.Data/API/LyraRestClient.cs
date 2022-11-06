using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Newtonsoft.Json;
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
        private readonly string _platform;
        private readonly string _appName;
        private readonly string _appVersion;
        private readonly string _url;

        private string _lastServer;
        private List<string> _servers;

        public string Host => _lastServer;
        private CancellationTokenSource _cancel;

        private TimeSpan _timeout;
        public void SetTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        private string GetUrlBase()
        {
            var rand = new Random();
            _lastServer = _servers.OrderBy(a => rand.Next()).First();
            return $"https://{_lastServer}/api/Node/";
        }

 /*       public LyraRestClient(string networkId)
        {
            _servers = networkId switch
            {
                "devnet" => new List<string> { 
                    "192.168.3.50:4504",
                    "192.168.3.77:4504",
                },
                "testnet" => new List<string> { 
                    "seed1.testnet.lyra.live:4504",
                    "seed2.testnet.lyra.live:4504",
                    "seed3.testnet.lyra.live:4504",
                    "seed4.testnet.lyra.live:4504",
                },
                "mainnet" => new List<string> {
                    "seed1.mainnet.lyra.live:5504",
                    "seed2.mainnet.lyra.live:5504",
                    "seed3.mainnet.lyra.live:5504",
                    "seed4.mainnet.lyra.live:5504",
                },
                _ => throw new Exception("Not valid network ID"),
            };

            _timeout = TimeSpan.FromSeconds(10);
            _cancel = new CancellationTokenSource();

            try
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) =>
                {
                    return true;
                };
            }
            catch { }
        }*/

        public LyraRestClient(string platform, string appName, string appVersion, string url)
        {
            _timeout = TimeSpan.FromSeconds(25);
            _platform = platform;
            _url = url;
            _appName = appName;
            _appVersion = appVersion;

            _cancel = new CancellationTokenSource();

            var uri = new Uri(url);
            if(uri.Port == 443 || uri.Port == 80)
                _servers = new List<string> { $"{uri.Host}" };
            else
                _servers = new List<string> { $"{uri.Host}:{uri.Port}" };

            if (url != null)
            {
                try
                {
                    System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        return true;
                    };
                }
                catch { }
            }
        }

        // This method must be in a class in a platform project, even if
        // the HttpClient object is constructed in a shared project.
        public HttpClientHandler GetInsecureHandler()
        {
            HttpClientHandler handler = new HttpClientHandler();
            try
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    //if (cert.Issuer.Equals("CN=localhost"))
                    //    return true;
                    //return errors == System.Net.Security.SslPolicyErrors.None;
                    return true;
                };
            }
            catch (Exception)
            {
                // wasm will throw platform unsupported exception
            }

            return handler;
        }

        private HttpClient CreateClient()
        {
            var uri = new Uri(GetUrlBase());
            if (uri.Host.Contains("lyra.live") && uri.Port == 443)
            {
                return new HttpClient()
                {
                    BaseAddress = uri,
                    Timeout = _timeout
            };
            }
            else
            {
                HttpClientHandler insecureHandler = GetInsecureHandler();
                return new HttpClient(insecureHandler)
                {
                    BaseAddress = uri,
                    Timeout = _timeout
                };
            }
        }

        public static LyraRestClient Create(string networkId, string platform, string appName, string appVersion, string apiUrl = null)
        {
            //if (apiUrl == null)
            //    return new LyraRestClient(networkId);

            var url = apiUrl ?? LyraGlobal.SelectNode(networkId) + "Node/";
            var restClient = new LyraRestClient(platform, appName, appVersion, url);

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

            //response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<T>();
                return result;
            }
            else
            {
                var resp = await response.Content.ReadAsStringAsync();
                throw new Exception($"Web Api Failed for {action}: {resp}");
            }
        }

        private async Task<T> PostAsync<T>(string action, object obj)
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
            {
                var resp = await response.Content.ReadAsStringAsync();
                throw new Exception($"Web Api Failed for {action}: {resp}");
            }
        }

        private async Task<T> GetAsync<T>(string action, Dictionary<string, string> args)
        {
            var url = $"{action}/?" + args?.Aggregate(new StringBuilder(),
                          (sb, kvp) => sb.AppendFormat("{0}{1}={2}",
                                       sb.Length > 0 ? "&" : "", kvp.Key, kvp.Value),
                          sb => sb.ToString());

            using var client = CreateClient();
            try
            {
                HttpResponseMessage response = await client.GetAsync(url, _cancel.Token);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsAsync<T>();
                    return result;
                }
                else
                {
                    var resp = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Web Api Failed for {action}: {resp}");
                }
            }
            catch(Exception ex)
            {
                throw new Exception($"Network error for {_url}: {ex.Message}");
            }
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
            {
                var resp = await response.Content.ReadAsStringAsync();
                throw new Exception($"Web Api Failed for {_url} -> {url}: {resp}");
            }
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
            {
                var resp = await response.Content.ReadAsStringAsync();
                throw new Exception($"Web Api Failed for {url}: {resp}");
            }
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
            try
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
            catch (Exception ex)
            {
                throw new Exception($"Network error for {_url}: {ex.Message}");
            }
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
            var ret = await GetBlockByUrlAsync($"GetBlockByHash/?AccountId={AccountId}&Signature={Signature}&Hash={Hash}");
            // add verify
            if(ret.Successful())
            {
                var blk = ret.GetBlock();
                if(blk != null && blk.Hash != Hash)
                {
                    throw new Exception($"Malfunction Node API of {_url} req {Hash} got {blk.Hash}");
                }
            }
            return ret;
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

        //public async Task<T?> GetLastBlockAsAsync<T>(string AccountId) where T : Block, IBrokerAccount;
        //{
        //    var ret = await GetLastBlockAsync(AccountId);
        //    if(ret.Successful())
        //    {
        //        var blk = ret.GetBlock();
        //        return blk as T;
        //    }
        //    return null;
        //}

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
            try
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
            catch (Exception ex)
            {
                throw new Exception($"Network error for {_url}: {ex.Message}");
            }
        }

        public async Task<BlockAPIResult> GetTokenGenesisBlockAsync(string AccountId, string TokenTicker, string Signature)
        {
            return await GetBlockByUrlAsync($"GetTokenGenesisBlock/?AccountId={AccountId}&TokenTicker={TokenTicker}&Signature={Signature}");
        }

        public async Task<GetListStringAPIResult> GetTokenNamesAsync(string? AccountId, string? Signature, string keyword)
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

        public async Task<SimpleJsonAPIResult> FindAllStakingsAsync(string pftid, DateTime timeBefore)
        {
            var args = new Dictionary<string, string>
            {
                { "pftid", pftid },
                { "timeBeforeTicks", timeBefore.Ticks.ToString() }
            };

            return await GetAsync<SimpleJsonAPIResult>("FindAllStakings2", args);
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

        public async Task<MultiBlockAPIResult> GetAllDexWalletsAsync(string owner)
        {
            return await GetMultiBlockByUrlAsync($"GetAllDexWallets/?owner=" + owner);
        }
        public async Task<BlockAPIResult> FindDexWalletAsync(string owner, string symbol, string provider)
        {
            var args = new Dictionary<string, string>
            {
                { "owner", owner },
                { "symbol", symbol },
                { "provider", provider }
            };

            return await GetAsync<BlockAPIResult>("FindDexWallet", args);
        }

        public async Task<MultiBlockAPIResult> GetAllDaosAsync(int page, int pageSize)
        {
            var args = new Dictionary<string, string>
            {
                { "page", page.ToString() },
                { "pageSize", pageSize.ToString() },
            };

            return await GetAsync<MultiBlockAPIResult>("GetAllDaos", args);
        }

        public async Task<BlockAPIResult> GetDaoByNameAsync(string name)
        {
            var args = new Dictionary<string, string>
            {
                { "name", name },
            };

            return await GetAsync<BlockAPIResult>("GetDaoByName", args);
        }

        public async Task<MultiBlockAPIResult> GetOtcOrdersByOwnerAsync(string accountId)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
            };

            return await GetAsync<MultiBlockAPIResult>("GetOtcOrdersByOwner", args);
        }

        public async Task<ContainerAPIResult> FindTradableOtcAsync()
        {
            var args = new Dictionary<string, string>
            {
               
            };

            return await GetAsync<ContainerAPIResult>("FindTradableOtc", args);
        }

        public Task<MultiBlockAPIResult> FindOtcTradeAsync(string accountId, bool onlyOpenTrade, int page, int pageSize)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
                { "onlyOpenTrade", onlyOpenTrade.ToString() },
                { "page", page.ToString() },
                { "pageSize", pageSize.ToString() },
            };

            return GetAsync<MultiBlockAPIResult>("FindOtcTrade", args);
        }

        public Task<MultiBlockAPIResult> FindOtcTradeByStatusAsync(string daoid, OTCTradeStatus status, int page, int pageSize)
        {
            var args = new Dictionary<string, string>
            {
                { "daoid", daoid },
                { "status", status.ToString() },
                { "page", page.ToString() },
                { "pageSize", pageSize.ToString() },
            };

            return GetAsync<MultiBlockAPIResult>("FindOtcTradeByStatus", args);
        }


        public Task<SimpleJsonAPIResult> GetOtcTradeStatsForUsersAsync(TradeStatsReq req)
        {
            return PostAsync<SimpleJsonAPIResult>("GetOtcTradeStatsForUsers", req);
        }

        public Task<MultiBlockAPIResult> FindAllVotesByDaoAsync(string daoid, bool openOnly)
        {
            var args = new Dictionary<string, string>
            {
                { "daoid", daoid },
                { "openOnly", openOnly.ToString() },
            };

            return GetAsync<MultiBlockAPIResult>("FindAllVotesByDao", args);
        }

        public Task<MultiBlockAPIResult> FindAllVoteForTradeAsync(string tradeid)
        {
            var args = new Dictionary<string, string>
            {
                { "tradeid", tradeid },
            };

            return GetAsync<MultiBlockAPIResult>("FindAllVoteForTrade", args);
        }

        public Task<SimpleJsonAPIResult> GetVoteSummaryAsync(string voteid)
        {
            var args = new Dictionary<string, string>
            {
                { "voteid", voteid },
            };

            return GetAsync<SimpleJsonAPIResult>("GetVoteSummary", args);
        }

        public async Task<BlockAPIResult> FindExecForVoteAsync(string voteid)
        {
            var args = new Dictionary<string, string>
            {
                { "voteid", voteid },
            };

            return await GetAsync<BlockAPIResult>("FindExecForVote", args);
        }

        public async Task<BlockAPIResult> GetDealerByAccountIdAsync(string accountId)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
            };

            return await GetAsync<BlockAPIResult>("GetDealerByAccountId", args);
        }

        public async Task<BlockAPIResult> FindNFTGenesisSendAsync(string accountId, string key)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
                { "key", key },
            };

            return await GetAsync<BlockAPIResult>("FindNFTGenesisSend", args);
        }
    }
}
