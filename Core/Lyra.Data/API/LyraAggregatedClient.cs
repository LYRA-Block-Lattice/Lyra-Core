using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API
{
    /// <summary>
    /// access all primary nodes to determinate state
    /// </summary>
    public class LyraAggregatedClient : ILyraAPI
    {
        private string _networkId;
        private bool _seedsOnly;

        private Dictionary<string, LyraRestClient> _primaryClients;

        public LyraRestClient SeedClient => LyraRestClient.Create(_networkId, Environment.OSVersion.Platform.ToString(), "LyraAggregatedClient", "1.0");

        public LyraAggregatedClient(string networkId, bool seedsOnly)
        {
            _networkId = networkId;
            _seedsOnly = seedsOnly;
        }

        // update it from node's json
        private string[] GetSeedNodes()
        {
            string[] seedNodes;
            if (_networkId == "devnet")
                seedNodes = new[] { "seed.devnet", "seed2.devnet", "seed3.devnet" };
            else if (_networkId == "testnet")
                seedNodes = new[] { "seed.testnet.lyra.live", "seed2.testnet.lyra.live", "seed3.testnet.lyra.live", "seed4.testnet.lyra.live" };
            else
                seedNodes = new[] { "seed1.mainnet.lyra.live", "seed2.mainnet.lyra.live", "seed3.mainnet.lyra.live", "seed4.mainnet.lyra.live" };
            return seedNodes;
        }

        public async Task InitAsync()
        {
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
                    Console.WriteLine("LyraAggregatedClient.InitAsync");
                    var apiClient = LyraRestClient.Create(_networkId, platform, appName, appVer);
                    currentBillBoard = await apiClient.GetBillBoardAsync();
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

                    if(_seedsOnly)
                    {
                        // create clients for primary nodes
                        _primaryClients = seedNodes
                            .Select(c => new
                            {
                                Kye = c,
                                Value = LyraRestClient.Create(_networkId, platform, appName, appVer, $"https://{c}:{peerPort}/api/Node/")
                            })
                            .ToDictionary(p => p.Kye, p => p.Value);
                    }
                    else
                    {
                        // create clients for primary nodes
                        _primaryClients = currentBillBoard.NodeAddresses
                            .Where(a => currentBillBoard.PrimaryAuthorizers.Contains(a.Key))
                            .Select(c => new
                            {
                                c.Key,
                                Value = LyraRestClient.Create(_networkId, platform, appName, appVer, $"https://{c.Value}:{peerPort}/api/Node/")
                            })
                            .ToDictionary(p => p.Key, p => p.Value);
                    }

                    if (_primaryClients.Count < 3)      // billboard not harvest address enough
                        await Task.Delay(2000);
                }
                catch(Exception exx)
                {
                    Console.WriteLine("Error init LyraAggregatedClient. Error: " + exx.ToString());
                    await Task.Delay(1000);
                    continue;
                }
            } while (currentBillBoard == null || _primaryClients.Count < 3);
        }

        public async Task<T> CheckResultAsync<T>(string name, List<Task<T>> tasks) where T: APIResult, new()
        {
            var expectedCount = LyraGlobal.GetMajority(tasks.Count);

            ISet<Task<T>> activeTasks = new HashSet<Task<T>>(tasks);
            while (activeTasks.Count > 0)
            {
                try
                {
                    try
                    {
                        await Task.WhenAny(activeTasks);
                    }
                    catch(Exception)
                    {

                    }

                    foreach (var t in activeTasks.Where(a => a.IsCompleted).ToList())
                        activeTasks.Remove(t);

                    var compeletedCount = tasks.Count(a => !(a.IsFaulted || a.IsCanceled) && a.IsCompleted);
                    Console.WriteLine($"Name: {name}, Completed: {compeletedCount} Expected: {expectedCount}");

                    if (compeletedCount >= expectedCount)
                    {
                        var coll = tasks.Where(a => !(a.IsFaulted || a.IsCanceled) && a.IsCompleted)
                            .Select(a => a.Result)
                            .GroupBy(b => b)
                            .Select(g => new
                            {
                                Data = g.Key,
                                Count = g.Count()
                            })
                            .OrderByDescending(x => x.Count);

                        var best = coll.First();

                        if (best.Count >= expectedCount)
                        {
                            var x = tasks.First(a => !(a.IsFaulted || a.IsCanceled) && a.IsCompleted && a.Result == best.Data);
                            return x.Result;
                        }
                        else
                        {
                            Console.WriteLine($"Result count: {best.Count} / {expectedCount}");
                        }
                    }
                }
                catch(Exception)
                {

                }
            }
            // No successful tasks
            return new T { ResultCode = APIResultCodes.APIRouteFailed };
        }

        public async Task<AuthorizationAPIResult> CancelTradeOrder(CancelTradeOrderBlock block)
        {
            return await SeedClient.CancelTradeOrder(block);
        }

        public async Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock block)
        {
            return await SeedClient.CreateToken(block);
        }

        public async Task<AuthorizationAPIResult> ExecuteTradeOrder(ExecuteTradeOrderBlock block)
        {
            return await SeedClient.ExecuteTradeOrder(block);
        }

        public async Task<AccountHeightAPIResult> GetAccountHeight(string AccountId)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetAccountHeight(AccountId)).ToList();

            return await CheckResultAsync("GetAccountHeight", tasks);
        }

        public async Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            return await SeedClient.GetActiveTradeOrders(AccountId, SellToken, BuyToken, OrderType, Signature);
        }

        public async Task<BillBoard> GetBillBoardAsync()
        {
            return await SeedClient.GetBillBoardAsync();
        }

        public async Task<BlockAPIResult> GetBlock(string Hash)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetBlock(Hash)).ToList();

            return await CheckResultAsync("GetBlock", tasks);
        }

        public async Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetBlockByHash(AccountId, Hash, Signature)).ToList();

            return await CheckResultAsync("GetBlockByHash", tasks);
        }

        public async Task<BlockAPIResult> GetBlockByIndex(string AccountId, long Index)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetBlockByIndex(AccountId, Index)).ToList();

            return await CheckResultAsync("GetBlockByIndex", tasks);
        }

        public async Task<BlockAPIResult> GetBlockBySourceHash(string sourceHash)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetBlockBySourceHash(sourceHash)).ToList();

            return await CheckResultAsync("GetBlockBySourceHash", tasks);
        }

        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRange(DateTime startTime, DateTime endTime)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetBlockHashesByTimeRange(startTime, endTime)).ToList();

            return await CheckResultAsync("GetBlockHashesByTimeRange", tasks);
        }

        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRange(long startTimeTicks, long endTimeTicks)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetBlockHashesByTimeRange(startTimeTicks, endTimeTicks)).ToList();

            return await CheckResultAsync("GetBlockHashesByTimeRange", tasks);
        }

        public async Task<MultiBlockAPIResult> GetBlocksByConsolidation(string AccountId, string Signature, string consolidationHash)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetBlocksByConsolidation(AccountId, Signature, consolidationHash)).ToList();

            return await CheckResultAsync("GetBlocksByConsolidation", tasks);
        }

        public async Task<MultiBlockAPIResult> GetBlocksByTimeRange(DateTime startTime, DateTime endTime)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetBlocksByTimeRange(startTime, endTime)).ToList();

            return await CheckResultAsync("GetBlocksByTimeRange", tasks);
        }

        public async Task<MultiBlockAPIResult> GetBlocksByTimeRange(long startTimeTicks, long endTimeTicks)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetBlocksByTimeRange(startTimeTicks, endTimeTicks)).ToList();

            return await CheckResultAsync("GetBlocksByTimeRange", tasks);
        }

        public async Task<MultiBlockAPIResult> GetConsolidationBlocks(string AccountId, string Signature, long startHeight, int count)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetConsolidationBlocks(AccountId, Signature, startHeight, count)).ToList();

            return await CheckResultAsync("GetConsolidationBlocks", tasks);
        }

        public async Task<string> GetDbStats()
        {
            return await SeedClient.GetDbStats();
        }

        public async Task<BlockAPIResult> GetLastBlock(string AccountId)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetLastBlock(AccountId)).ToList();

            return await CheckResultAsync("GetLastBlock", tasks);
        }

        public async Task<BlockAPIResult> GetLastConsolidationBlock()
        {
            var tasks = _primaryClients.Select(client => client.Value.GetLastConsolidationBlock()).ToList();

            return await CheckResultAsync("GetLastConsolidationBlock", tasks);
        }

        public async Task<BlockAPIResult> GetLastServiceBlock()
        {
            var tasks = _primaryClients.Select(client => client.Value.GetLastServiceBlock()).ToList();

            return await CheckResultAsync("GetLastServiceBlock", tasks);
        }

        public async Task<BlockAPIResult> GetLyraTokenGenesisBlock()
        {
            var tasks = _primaryClients.Select(client => client.Value.GetLyraTokenGenesisBlock()).ToList();

            return await CheckResultAsync("GetLyraTokenGenesisBlock", tasks);
        }

        public async Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetNonFungibleTokens(AccountId, Signature)).ToList();

            return await CheckResultAsync("GetNonFungibleTokens", tasks);
        }

        public async Task<BlockAPIResult> GetServiceBlockByIndex(string blockType, long Index)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetServiceBlockByIndex(blockType, Index)).ToList();

            return await CheckResultAsync("GetServiceBlockByIndex", tasks);
        }

        public async Task<BlockAPIResult> GetServiceGenesisBlock()
        {
            var tasks = _primaryClients.Select(client => client.Value.GetServiceGenesisBlock()).ToList();

            return await CheckResultAsync("GetServiceGenesisBlock", tasks);
        }

        public async Task<AccountHeightAPIResult> GetSyncHeight()
        {
            var tasks = _primaryClients.Select(client => client.Value.GetSyncHeight()).ToList();

            return await CheckResultAsync("GetSyncHeight", tasks);
        }

        public async Task<GetSyncStateAPIResult> GetSyncState()
        {
            var tasks = _primaryClients.Select(client => client.Value.GetSyncState()).ToList();

            return await CheckResultAsync("GetSyncState", tasks);
        }

        public async Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetTokenGenesisBlock(AccountId, TokenTicker, Signature)).ToList();

            return await CheckResultAsync("GetTokenGenesisBlock", tasks);
        }

        public async Task<GetListStringAPIResult> GetTokenNames(string AccountId, string Signature, string keyword)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetTokenNames(AccountId, Signature, keyword)).ToList();

            return await CheckResultAsync("GetTokenNames", tasks);
        }

        public async Task<List<TransStats>> GetTransStatsAsync()
        {
            return await SeedClient.GetTransStatsAsync();
        }

        public async Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetVersion(apiVersion, appName, appVersion)).ToList();

            return await CheckResultAsync("GetVersion", tasks);
        }

        public async Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            return await SeedClient.ImportAccount(block);
        }

        public async Task<NewFeesAPIResult> LookForNewFees(string AccountId, string Signature)
        {
            return await SeedClient.LookForNewFees(AccountId, Signature);
        }

        public async Task<TradeAPIResult> LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            return await SeedClient.LookForNewTrade(AccountId, BuyTokenCode, SellTokenCode, Signature);
        }

        public async Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            return await SeedClient.LookForNewTransfer(AccountId, Signature);
        }

        public async Task<NewTransferAPIResult2> LookForNewTransfer2(string AccountId, string Signature)
        {
            return await SeedClient.LookForNewTransfer2(AccountId, Signature);
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock block)
        {
            return await SeedClient.OpenAccountWithGenesis(block);
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            return await SeedClient.OpenAccountWithImport(block);
        }

        public async Task<AuthorizationAPIResult> ReceiveFee(ReceiveAuthorizerFeeBlock block)
        {
            return await SeedClient.ReceiveFee(block);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock block)
        {
            return await SeedClient.ReceiveTransfer(block);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock block)
        {
            return await SeedClient.ReceiveTransferAndOpenAccount(block);
        }

        public async Task<TransactionsAPIResult> SearchTransactions(string accountId, long startTimeTicks, long endTimeTicks, int count)
        {
            return await SeedClient.SearchTransactions(accountId, startTimeTicks, endTimeTicks, count);
        }

        public async Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock block)
        {
            return await SeedClient.SendTransfer(block);
        }

        public async Task<AuthorizationAPIResult> Trade(TradeBlock block)
        {
            return await SeedClient.Trade(block);
        }

        public async Task<TradeOrderAuthorizationAPIResult> TradeOrder(TradeOrderBlock block)
        {
            return await SeedClient.TradeOrder(block);
        }

        public List<Voter> GetVoters(VoteQueryModel model)
        {
            List<Voter> result = null;
            var t = Task.Run(async () => { result = await SeedClient.GetVotersAsync(model); });
            Task.WaitAll(t);
            return result;
        }

        public List<Vote> FindVotes(VoteQueryModel model)
        {
            List<Vote> result = null;
            var t = Task.Run(async () => { result = await SeedClient.FindVotesAsync(model); });
            Task.WaitAll(t);
            return result;
        }

        public FeeStats GetFeeStats()
        {
            FeeStats result = null;
            var t = Task.Run(async () => { result = await SeedClient.GetFeeStatsAsync(); });
            Task.WaitAll(t);
            return result;
        }

        public async Task<PoolInfoAPIResult> GetPool(string token0, string token1)
        {
            var tasks = _primaryClients.Select(client => client.Value.GetPool(token0, token1)).ToList();

            return await CheckResultAsync("", tasks);
        }
    }
}
