﻿using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API
{
    /// <summary>
    /// access all primary nodes to determinate state
    /// </summary>
    public class LyraAggregatedClient : ILyraAPI
    {
        private readonly string _networkId;
        private bool _seedsOnly;
        private string _poswallet;

        private List<LyraRestClient> _primaryClients;

        public LyraRestClient SeedClient => LyraRestClient.Create(_networkId, Environment.OSVersion.Platform.ToString(), "LyraAggregatedClient", "1.0");

        public LyraAggregatedClient(string networkId, bool seedsOnly, string poswallet)
        {
            _networkId = networkId;
            _seedsOnly = seedsOnly;
            _poswallet = poswallet;
        }

        // update it from node's json
        public string[] GetSeedNodes()
        {
            return Enumerable.Range(1, 4).Select(a => $"seed{a}.{_networkId}.lyra.live").ToArray();
        }

        public async Task InitAsync()
        {
            ServicePointManager.DefaultConnectionLimit = 30;
            var platform = Environment.OSVersion.Platform.ToString();
            var appName = "LyraAggregatedClient";
            var appVer = "1.0";

            ushort peerPort = 4504;
            if (_networkId == "mainnet")
                peerPort = 5504;

            // get nodes list (from billboard)
            var seedNodes = GetSeedNodes();

            var seeds = seedNodes.Select(a => LyraRestClient.Create(_networkId, platform, appName, appVer, $"https://{a}:{peerPort}/api/Node/")).ToList();

            BillBoard currentBillBoard = null;
            do
            {
                try
                {
                    Console.WriteLine($"LyraAggregatedClient.InitAsync Seed Only: {_seedsOnly}");
                    var rand = new Random();
                    foreach (var sd in seedNodes.OrderBy(a => rand.Next()))
                    {
                        try
                        {
                            var apiClient = LyraRestClient.Create(_networkId, platform, appName, appVer, $"https://{sd}:{peerPort}/api/Node/");
                            currentBillBoard = await apiClient.GetBillBoardAsync();
                            Console.WriteLine($"LyraAggregatedClient.InitAsync Got billboard from {sd}");
                            break;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("LyraAggregatedClient.InitAsync: " + e.Message);
                        }
                    }

                    //var bbtasks = seeds.Select(client => client.GetBillBoardAsync()).ToList();
                    //try
                    //{
                    //    await Task.WhenAll(bbtasks);
                    //}
                    //catch (Exception ex)
                    //{
                    //    Console.WriteLine($"In LyraAggregatedClient.InitAsync: " + ex.Message);
                    //}
                    //var goodbb = bbtasks.Where(a => !(a.IsFaulted || a.IsCanceled) && a.IsCompleted && a.Result != null).Select(a => a.Result).ToList();

                    //if (goodbb.Count == 0)
                    //    continue;

                    //// pickup best result
                    //var best = goodbb
                    //        .GroupBy(b => b.CurrentLeader)
                    //        .Select(g => new
                    //        {
                    //            Data = g.Key,
                    //            Count = g.Count()
                    //        })
                    //        .OrderByDescending(x => x.Count)
                    //        .First();

                    //if (best.Count >= seedNodes.Length - 2 && !string.IsNullOrWhiteSpace(best.Data))
                    //{
                    //    var r = new Random();
                    //    currentBillBoard = goodbb.ElementAt(r.Next(0, goodbb.Count()));
                    //    //currentBillBoard = goodbb.First(a => a.CurrentLeader == best.Data);
                    //}
                    //else
                    //{
                    //    await Task.Delay(2000);
                    //    continue;
                    //}

                    if (_seedsOnly)
                    {
                        // create clients for primary nodes
                        _primaryClients = seedNodes
                            .Select(c => LyraRestClient.Create(_networkId, platform, appName, appVer, $"https://{c}:{peerPort}/api/Node/"))
                            .ToList();
                    }
                    else if (currentBillBoard == null)
                    {
                        Console.WriteLine("Error init LyraAggregatedClient. Billboard is null.");
                        await Task.Delay(1000);
                        continue;
                    }
                    else
                    {
                        // create clients for primary nodes
                        _primaryClients = currentBillBoard.NodeAddresses
                            .Where(a => currentBillBoard.PrimaryAuthorizers.Contains(a.Key))
                            .Where(a => a.Key != _poswallet)
                            .Select(c => LyraRestClient.Create(_networkId, platform, appName, appVer, $"https://{c.Value}:{peerPort}/api/Node/"))
                            //.Take(7)    // don't spam the whole network
                            .ToList();
                    }

                    if (_primaryClients.Count < 2)      // billboard not harvest address enough
                        await Task.Delay(2000);

                    foreach (var primaryClient in _primaryClients)
                        primaryClient.SetTimeout(TimeSpan.FromSeconds(5));
                }
                catch (Exception exx)
                {
                    Console.WriteLine("Error init LyraAggregatedClient. Error: " + exx.ToString());
                    await Task.Delay(1000);
                    continue;
                }
            } while (currentBillBoard == null || _primaryClients.Count < 2);
        }

        public void ReBase(bool toSeedOnly)
        {
            //_seedsOnly = toSeedOnly;
            //if (toSeedOnly)
            //    _baseIndex = _baseIndex++ % 4;
            //else
            //    throw new InvalidOperationException("Only rebase to seed supported.");

            //// find the highest chain from seeds
            //var tasks = _primaryClients.ToDictionary(client => client.Key, client => client.GetSyncState());
            //try
            //{
            //    await Task.WhenAll(tasks.Values);
            //}
            //catch { }

            //var highest = tasks.Where(a => !(a.Value.IsFaulted || a.Value.IsCanceled) && a.Value.IsCompleted)
            //    .Select(x => x.Value.Result)
            //    .OrderByDescending(o => o.Status.totalBlockCount)
            //    .First();

            //for(int i = 0; i < tasks.Count; i++)
            //{
            //    if(tasks[i].IsCompleted && tasks[i].Result.Status.totalBlockCount == highest.Status.totalBlockCount)
            //    {
            //        _baseIndex = i;
            //        _baseClient = _primaryClients.ToArray()[i].Value;
            //        break;
            //    }
            //}
            ////
        }

        public Task<ResultOrException<T>[]> WhenAllOrExceptionAsync<T>(IEnumerable<Task<T>> tasks)
        {
            return Task.WhenAll(tasks.Select(task => WrapResultOrExceptionAsync(task)));
        }

        private async Task<ResultOrException<T>> WrapResultOrExceptionAsync<T>(Task<T> task)
        {
            try
            {
                var result = await task;
                return new ResultOrException<T>(result);
            }
            catch (Exception ex)
            {
                return new ResultOrException<T>(ex);
            }
        }


        public class ResultOrException<T>
        {
            public ResultOrException(T result)
            {
                IsSuccess = true;
                Result = result;
            }

            public ResultOrException(Exception ex)
            {
                IsSuccess = false;
                Exception = ex;
            }

            public bool IsSuccess { get; }
            public T Result { get; }
            public Exception Exception { get; }
        }

        public async Task<T> CheckResultAsync<T>(string name, List<Task<T>> taskss, string tag = null) where T : APIResult, new()
        {
            var results = await WhenAllOrExceptionAsync(taskss);

            var expectedCount = LyraGlobal.GetMajority(taskss.Count);
            if (_seedsOnly)    // seed stage
                expectedCount = 2;

            var compeletedCount = results.Count(a => a.IsSuccess);
            //Console.WriteLine($"Name: {name}, Completed: {compeletedCount} Expected: {expectedCount}");

            if (compeletedCount >= expectedCount)
            {
                var coll = results.Where(a => a.IsSuccess)
                    .Select(a => a.Result)
                    .GroupBy(b => b)
                    .Select(g => new
                    {
                        Data = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count);

                var best = coll.First();

                // hack. testnet has a bad block. nutralize it.
                if (best.Count >= expectedCount)
                {
                    var x = results.First(a => a.IsSuccess && a.Result == best.Data);
                    return x.Result;
                }
                else
                {
                    Console.WriteLine($"Result count: {best.Count} / {expectedCount} / {taskss.Count}");
                    // print the unconsist ones
                }
            }

            var failedResult = results.Where(a => !a.IsSuccess);
            string msg = $"Success {compeletedCount}/{expectedCount}/{taskss.Count}, ";
            if(failedResult.Any())
            {
                var failed = failedResult
                    .Select(a => a.Exception.Message)
                    .Aggregate((a, b) => a + "," + b);
                msg += $"Failed: {failed}";
            }
            else
            {
                msg += $"Failed: None. Db inconsist for {tag}.";
                await InitAsync();
            }
            Console.WriteLine(msg);

            return new T { 
                ResultCode = APIResultCodes.APIRouteFailed,
                ResultMessage = msg,
            };
        }

        public async Task<AuthorizationAPIResult> CancelTradeOrderAsync(CancelTradeOrderBlock block)
        {
            return await SeedClient.CancelTradeOrderAsync(block);
        }

        public async Task<AuthorizationAPIResult> CreateTokenAsync(TokenGenesisBlock block)
        {
            return await SeedClient.CreateTokenAsync(block);
        }

        public async Task<AuthorizationAPIResult> ExecuteTradeOrderAsync(ExecuteTradeOrderBlock block)
        {
            return await SeedClient.ExecuteTradeOrderAsync(block);
        }

        public async Task<AccountHeightAPIResult> GetAccountHeightAsync(string AccountId)
        {
            var tasks = _primaryClients.Select(client => client.GetAccountHeightAsync(AccountId)).ToList();

            return await CheckResultAsync("GetAccountHeight", tasks);
        }

        public async Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrdersAsync(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            return await SeedClient.GetActiveTradeOrdersAsync(AccountId, SellToken, BuyToken, OrderType, Signature);
        }

        public async Task<BillBoard> GetBillBoardAsync()
        {
            return await SeedClient.GetBillBoardAsync();
        }

        public async Task<BlockAPIResult> GetBlockAsync(string Hash)
        {
            var tasks = _primaryClients.Select(client => client.GetBlockAsync(Hash)).ToList();

            return await CheckResultAsync("GetBlock", tasks);
        }

        public async Task<BlockAPIResult> GetBlockByHashAsync(string AccountId, string Hash, string Signature)
        {
            var tasks = _primaryClients.Select(client => client.GetBlockByHashAsync(AccountId, Hash, Signature)).ToList();

            return await CheckResultAsync("GetBlockByHash", tasks, Hash);
        }

        public async Task<BlockAPIResult> GetBlockByIndexAsync(string AccountId, long Index)
        {
            var tasks = _primaryClients.Select(client => client.GetBlockByIndexAsync(AccountId, Index)).ToList();

            return await CheckResultAsync("GetBlockByIndex", tasks);
        }

        public async Task<BlockAPIResult> GetBlockBySourceHashAsync(string sourceHash)
        {
            var tasks = _primaryClients.Select(client => client.GetBlockBySourceHashAsync(sourceHash)).ToList();

            return await CheckResultAsync("GetBlockBySourceHash", tasks);
        }

        public async Task<MultiBlockAPIResult> GetBlocksByRelatedTxAsync(string sourceHash)
        {
            var tasks = _primaryClients.Select(client => client.GetBlocksByRelatedTxAsync(sourceHash)).ToList();

            return await CheckResultAsync("GetBlocksByRelatedTxAsync", tasks);
        }

        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            var tasks = _primaryClients.Select(client => client.GetBlockHashesByTimeRangeAsync(startTime, endTime)).ToList();

            return await CheckResultAsync("GetBlockHashesByTimeRange", tasks);
        }

        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRangeAsync(long startTimeTicks, long endTimeTicks)
        {
            var tasks = _primaryClients.Select(client => client.GetBlockHashesByTimeRangeAsync(startTimeTicks, endTimeTicks)).ToList();

            return await CheckResultAsync("GetBlockHashesByTimeRange", tasks);
        }

        public async Task<MultiBlockAPIResult> GetBlocksByConsolidationAsync(string AccountId, string Signature, string consolidationHash)
        {
            var tasks = _primaryClients.Select(client => client.GetBlocksByConsolidationAsync(AccountId, Signature, consolidationHash)).ToList();

            return await CheckResultAsync("GetBlocksByConsolidation", tasks);
        }

        public async Task<MultiBlockAPIResult> GetBlocksByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            var tasks = _primaryClients.Select(client => client.GetBlocksByTimeRangeAsync(startTime, endTime)).ToList();

            return await CheckResultAsync("GetBlocksByTimeRange", tasks);
        }

        public async Task<MultiBlockAPIResult> GetBlocksByTimeRangeAsync(long startTimeTicks, long endTimeTicks)
        {
            var tasks = _primaryClients.Select(client => client.GetBlocksByTimeRangeAsync(startTimeTicks, endTimeTicks)).ToList();

            return await CheckResultAsync("GetBlocksByTimeRange", tasks);
        }

        public async Task<MultiBlockAPIResult> GetConsolidationBlocksAsync(string AccountId, string Signature, long startHeight, int count)
        {
            var tasks = _primaryClients.Select(client => client.GetConsolidationBlocksAsync(AccountId, Signature, startHeight, count)).ToList();

            return await CheckResultAsync("GetConsolidationBlocks", tasks);
        }

        public async Task<string> GetDbStatsAsync()
        {
            return await SeedClient.GetDbStatsAsync();
        }

        public async Task<BlockAPIResult> GetLastBlockAsync(string AccountId)
        {
            var tasks = _primaryClients.Select(client => client.GetLastBlockAsync(AccountId)).ToList();

            return await CheckResultAsync("GetLastBlock", tasks);
        }

        public async Task<BlockAPIResult> GetLastConsolidationBlockAsync()
        {
            var tasks = _primaryClients.Select(client => client.GetLastConsolidationBlockAsync()).ToList();

            return await CheckResultAsync("GetLastConsolidationBlock", tasks);
        }

        public async Task<BlockAPIResult> GetLastServiceBlockAsync()
        {
            var tasks = _primaryClients.Select(client => client.GetLastServiceBlockAsync()).ToList();

            return await CheckResultAsync("GetLastServiceBlock", tasks);
        }

        public async Task<BlockAPIResult> GetLyraTokenGenesisBlockAsync()
        {
            var tasks = _primaryClients.Select(client => client.GetLyraTokenGenesisBlockAsync()).ToList();

            return await CheckResultAsync("GetLyraTokenGenesisBlock", tasks);
        }

        public async Task<NonFungibleListAPIResult> GetNonFungibleTokensAsync(string AccountId, string Signature)
        {
            var tasks = _primaryClients.Select(client => client.GetNonFungibleTokensAsync(AccountId, Signature)).ToList();

            return await CheckResultAsync("GetNonFungibleTokens", tasks);
        }

        public async Task<BlockAPIResult> GetServiceBlockByIndexAsync(string blockType, long Index)
        {
            var tasks = _primaryClients.Select(client => client.GetServiceBlockByIndexAsync(blockType, Index)).ToList();

            return await CheckResultAsync("GetServiceBlockByIndex", tasks);
        }

        public async Task<BlockAPIResult> GetServiceGenesisBlockAsync()
        {
            var tasks = _primaryClients.Select(client => client.GetServiceGenesisBlockAsync()).ToList();

            return await CheckResultAsync("GetServiceGenesisBlock", tasks);
        }

        public async Task<AccountHeightAPIResult> GetSyncHeightAsync()
        {
            var tasks = _primaryClients.Select(client => client.GetSyncHeightAsync()).ToList();

            return await CheckResultAsync("GetSyncHeight", tasks);
        }

        public async Task<GetSyncStateAPIResult> GetSyncStateAsync()
        {
            var tasks = _primaryClients.Select(client => client.GetSyncStateAsync()).ToList();

            return await CheckResultAsync("GetSyncState", tasks);
        }

        public async Task<BlockAPIResult> GetTokenGenesisBlockAsync(string AccountId, string TokenTicker, string Signature)
        {
            var tasks = _primaryClients.Select(client => client.GetTokenGenesisBlockAsync(AccountId, TokenTicker, Signature)).ToList();

            return await CheckResultAsync("GetTokenGenesisBlock", tasks);
        }

        public async Task<GetListStringAPIResult> GetTokenNamesAsync(string? AccountId, string? Signature, string keyword)
        {
            var tasks = _primaryClients.Select(client => client.GetTokenNamesAsync(AccountId, Signature, keyword)).ToList();

            return await CheckResultAsync("GetTokenNames", tasks);
        }

        public async Task<List<TransStats>> GetTransStatsAsync()
        {
            return await SeedClient.GetTransStatsAsync();
        }

        public async Task<GetVersionAPIResult> GetVersionAsync(int apiVersion, string appName, string appVersion)
        {
            var tasks = _primaryClients.Select(client => client.GetVersionAsync(apiVersion, appName, appVersion)).ToList();

            return await CheckResultAsync("GetVersion", tasks);
        }

        public async Task<AuthorizationAPIResult> ImportAccountAsync(ImportAccountBlock block)
        {
            return await SeedClient.ImportAccountAsync(block);
        }

        public async Task<NewFeesAPIResult> LookForNewFeesAsync(string AccountId, string Signature)
        {
            return await SeedClient.LookForNewFeesAsync(AccountId, Signature);
        }

        public async Task<TradeAPIResult> LookForNewTradeAsync(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            return await SeedClient.LookForNewTradeAsync(AccountId, BuyTokenCode, SellTokenCode, Signature);
        }

        public async Task<NewTransferAPIResult> LookForNewTransferAsync(string AccountId, string Signature)
        {
            return await SeedClient.LookForNewTransferAsync(AccountId, Signature);
        }

        public async Task<NewTransferAPIResult2> LookForNewTransfer2Async(string AccountId, string Signature)
        {
            return await SeedClient.LookForNewTransfer2Async(AccountId, Signature);
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithGenesisAsync(LyraTokenGenesisBlock block)
        {
            return await SeedClient.OpenAccountWithGenesisAsync(block);
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithImportAsync(OpenAccountWithImportBlock block)
        {
            return await SeedClient.OpenAccountWithImportAsync(block);
        }

        public async Task<AuthorizationAPIResult> ReceiveFeeAsync(ReceiveNodeProfitBlock block)
        {
            return await SeedClient.ReceiveFeeAsync(block);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransferAsync(ReceiveTransferBlock block)
        {
            return await SeedClient.ReceiveTransferAsync(block);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccountAsync(OpenWithReceiveTransferBlock block)
        {
            return await SeedClient.ReceiveTransferAndOpenAccountAsync(block);
        }

        public async Task<TransactionsAPIResult> SearchTransactionsAsync(string accountId, long startTimeTicks, long endTimeTicks, int count)
        {
            return await SeedClient.SearchTransactionsAsync(accountId, startTimeTicks, endTimeTicks, count);
        }

        public async Task<AuthorizationAPIResult> SendTransferAsync(SendTransferBlock block)
        {
            return await SeedClient.SendTransferAsync(block);
        }

        public async Task<AuthorizationAPIResult> TradeAsync(TradeBlock block)
        {
            return await SeedClient.TradeAsync(block);
        }

        public async Task<TradeOrderAuthorizationAPIResult> TradeOrderAsync(TradeOrderBlock block)
        {
            return await SeedClient.TradeOrderAsync(block);
        }

        public List<Voter> GetVoters(VoteQueryModel model)
        {
            return SeedClient.GetVoters(model);
        }

        public List<Vote> FindVotes(VoteQueryModel model)
        {
            return SeedClient.FindVotes(model);
        }

        public Task<FeeStats> GetFeeStatsAsync()
        {
            return SeedClient.GetFeeStatsAsync();
        }

        public async Task<PoolInfoAPIResult> GetPoolAsync(string token0, string token1)
        {
            var tasks = _primaryClients.Select(client => client.GetPoolAsync(token0, token1)).ToList();

            return await CheckResultAsync("", tasks);
        }

        public Task<MultiBlockAPIResult> GetAllBrokerAccountsForOwnerAsync(string ownerAccount)
        {
            throw new NotImplementedException();
        }

        public List<Staker> FindAllStakings(string pftid, DateTime timeBefore)
        {
            throw new NotImplementedException();
        }

        Task<ProfitingStats> INodeAPI.GetAccountStatsAsync(string accountId, DateTime begin, DateTime end)
        {
            throw new NotImplementedException();
        }

        public Task<ProfitingStats> GetBenefitStatsAsync(string pftid, string stkid, DateTime begin, DateTime end)
        {
            return SeedClient.GetBenefitStatsAsync(pftid, stkid, begin, end);
        }

        public Task<List<Profiting>> FindAllProfitingAccountsAsync(DateTime begin, DateTime end)
        {
            return SeedClient.FindAllProfitingAccountsAsync(begin, end);
        }

        public Task<ProfitingGenesis> FindProfitingAccountsByNameAsync(string Name)
        {
            return SeedClient.FindProfitingAccountsByNameAsync(Name);
        }

        public Task<PendingStats> GetPendingStatsAsync(string accountId)
        {
            return SeedClient.GetPendingStatsAsync(accountId);
        }

        public Task<MultiBlockAPIResult> GetAllDexWalletsAsync(string owner)
        {
            return SeedClient.GetAllDexWalletsAsync(owner);
        }

        public Task<BlockAPIResult> FindDexWalletAsync(string owner, string symbol, string provider)
        {
            return SeedClient.FindDexWalletAsync(owner, symbol, provider);
        }

        public Task<MultiBlockAPIResult> GetAllDaosAsync(int page, int pageSize)
        {
            return SeedClient.GetAllDaosAsync(page, pageSize);
        }

        public Task<BlockAPIResult> GetDaoByNameAsync(string name)
        {
            return SeedClient.GetDaoByNameAsync(name);
        }

        public Task<MultiBlockAPIResult> GetOtcOrdersByOwnerAsync(string accountId)
        {
            return SeedClient.GetOtcOrdersByOwnerAsync(accountId);
        }

        public Task<ContainerAPIResult> FindTradableOtcAsync()
        {
            return SeedClient.FindTradableOtcAsync();
        }

        public Task<MultiBlockAPIResult> FindOtcTradeAsync(string accountId, bool onlyOpenTrade, int page, int pageSize)
        {
            return SeedClient.FindOtcTradeAsync(accountId, onlyOpenTrade, page, pageSize);
        }

        public Task<MultiBlockAPIResult> FindOtcTradeByStatusAsync(string daoid, OTCTradeStatus status, int page, int pageSize)
        {
            return SeedClient.FindOtcTradeByStatusAsync(daoid, status, page, pageSize);
        }

        public Task<MultiBlockAPIResult> FindAllVotesByDaoAsync(string daoid, bool openOnly)
        {
            return SeedClient.FindAllVotesByDaoAsync(daoid, openOnly);
        }

        public Task<MultiBlockAPIResult> FindAllVoteForTradeAsync(string tradeid)
        {
            return SeedClient.FindAllVoteForTradeAsync(tradeid);
        }

        public Task<SimpleJsonAPIResult> GetVoteSummaryAsync(string voteid)
        {
            return SeedClient.GetVoteSummaryAsync(voteid);
        }

        public Task<BlockAPIResult> FindExecForVoteAsync(string voteid)
        {
            return SeedClient.FindExecForVoteAsync(voteid);
        }
    }
}
