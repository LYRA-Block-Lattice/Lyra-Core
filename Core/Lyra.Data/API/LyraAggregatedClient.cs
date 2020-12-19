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
    public class LyraAggregatedClient : INodeAPI, INodeTransactionAPI, INodeDexAPI
    {
        private string _networkId;
        private Dictionary<string, LyraRestClient> _primaryClients;

        public LyraAggregatedClient(string networkId)
        {
            this._networkId = networkId;
        }

        public async Task InitAsync()
        {
            var platform = Environment.OSVersion.Platform.ToString();
            var appName = "LyraAggregatedClient";
            var appVer = "1.0";

            ushort peerPort = 4504;
            if (_networkId == "mainnet")
                peerPort = 5504;

            // get latest service block
            var seedApiNodeClient = LyraRestClient.Create(_networkId, platform, appName, appVer);

            // get nodes list (from billboard)
            var seedBillBoard = await seedApiNodeClient.GetBillBoardAsync();

            // create clients for primary nodes
            _primaryClients = seedBillBoard.NodeAddresses
                .Where(a => seedBillBoard.PrimaryAuthorizers.Contains(a.Key))
                .Select(c => new
                {
                    c.Key,
                    Value = LyraRestClient.Create(_networkId, platform, appName, appVer, $"https://{c.Value}:{peerPort}/api/Node/")
                })
                .ToDictionary(p => p.Key, p => p.Value);
        }

        public async Task<BlockAPIResult> BlockResultAsync(List<Task<BlockAPIResult>> tasks)
        {
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception e)
            {
                // do nothing?                
            }

            var goodResults = tasks.Where(a => a.IsCompletedSuccessfully)
                .Select(a => a.Result)
                .Where(a => a.ResultCode == APIResultCodes.Success);

            var goodCount = goodResults.Count();
            if (goodCount >= LyraGlobal.GetMajority(_primaryClients.Count))
            {
                var best = goodResults
                    .GroupBy(b => b.BlockData)
                    .Select(g => new
                    {
                        Data = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .First();

                if (best.Count >= LyraGlobal.GetMajority(_primaryClients.Count))
                {
                    var x = goodResults.First(a => a.BlockData == best.Data);
                    return x;
                }
            }

            return new BlockAPIResult { ResultCode = APIResultCodes.APIRouteFailed };
        }

        public Task<APIResult> CancelExchangeOrder(string AccountId, string Signature, string cancelKey)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> CancelTradeOrder(CancelTradeOrderBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<ExchangeAccountAPIResult> CloseExchangeAccount(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<ExchangeAccountAPIResult> CreateExchangeAccount(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> ExecuteTradeOrder(ExecuteTradeOrderBlock block)
        {
            throw new NotImplementedException();
        }

        public List<Vote> FindVotes(VoteQueryModel model)
        {
            throw new NotImplementedException();
        }

        public Task<AccountHeightAPIResult> GetAccountHeight(string AccountId)
        {
            throw new NotImplementedException();
        }

        public Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<BillBoard> GetBillBoardAsync()
        {
            throw new NotImplementedException();
        }

        public async Task<BlockAPIResult> GetBlock(string Hash)
        {
            var tasks = _primaryClients.Select(async client => await client.Value.GetBlock(Hash)).ToList();

            return await BlockResultAsync(tasks);
        }

        public async Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            var tasks = _primaryClients.Select(async client => await client.Value.GetBlockByHash(AccountId, Hash, Signature)).ToList();

            return await BlockResultAsync(tasks);
        }

        public async Task<BlockAPIResult> GetBlockByIndex(string AccountId, long Index)
        {
            var tasks = _primaryClients.Select(async client => await client.Value.GetBlockByIndex(AccountId, Index)).ToList();

            return await BlockResultAsync(tasks);
        }

        public async Task<BlockAPIResult> GetBlockBySourceHash(string sourceHash)
        {
            var tasks = _primaryClients.Select(async client => await client.Value.GetBlockBySourceHash(sourceHash)).ToList();

            return await BlockResultAsync(tasks);
        }

        public Task<GetListStringAPIResult> GetBlockHashesByTimeRange(DateTime startTime, DateTime endTime)
        {
            throw new NotImplementedException();
        }

        public Task<GetListStringAPIResult> GetBlockHashesByTimeRange(long startTimeTicks, long endTimeTicks)
        {
            throw new NotImplementedException();
        }

        public Task<MultiBlockAPIResult> GetBlocksByConsolidation(string AccountId, string Signature, string consolidationHash)
        {
            throw new NotImplementedException();
        }

        public Task<MultiBlockAPIResult> GetBlocksByTimeRange(DateTime startTime, DateTime endTime)
        {
            throw new NotImplementedException();
        }

        public Task<MultiBlockAPIResult> GetBlocksByTimeRange(long startTimeTicks, long endTimeTicks)
        {
            throw new NotImplementedException();
        }

        public Task<MultiBlockAPIResult> GetConsolidationBlocks(string AccountId, string Signature, long startHeight, int count)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetDbStats()
        {
            throw new NotImplementedException();
        }

        public Task<ExchangeBalanceAPIResult> GetExchangeBalance(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        public FeeStats GetFeeStats()
        {
            throw new NotImplementedException();
        }

        public async Task<BlockAPIResult> GetLastBlock(string AccountId)
        {
            var tasks = _primaryClients.Select(async client => await client.Value.GetLastBlock(AccountId)).ToList();

            return await BlockResultAsync(tasks);
        }

        public async Task<BlockAPIResult> GetLastConsolidationBlock()
        {
            var tasks = _primaryClients.Select(async client => await client.Value.GetLastConsolidationBlock()).ToList();

            return await BlockResultAsync(tasks);
        }

        public async Task<BlockAPIResult> GetLastServiceBlock()
        {
            var tasks = _primaryClients.Select(async client => await client.Value.GetLastServiceBlock()).ToList();

            return await BlockResultAsync(tasks);
        }

        public async Task<BlockAPIResult> GetLyraTokenGenesisBlock()
        {
            var tasks = _primaryClients.Select(async client => await client.Value.GetLyraTokenGenesisBlock()).ToList();

            return await BlockResultAsync(tasks);
        }

        public Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<List<ExchangeOrder>> GetOrdersForAccount(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        public async Task<BlockAPIResult> GetServiceBlockByIndex(string blockType, long Index)
        {
            var tasks = _primaryClients.Select(async client => await client.Value.GetServiceBlockByIndex(blockType, Index)).ToList();

            return await BlockResultAsync(tasks);
        }

        public async Task<BlockAPIResult> GetServiceGenesisBlock()
        {
            var tasks = _primaryClients.Select(async client => await client.Value.GetServiceGenesisBlock()).ToList();

            return await BlockResultAsync(tasks);
        }

        public Task<AccountHeightAPIResult> GetSyncHeight()
        {
            throw new NotImplementedException();
        }

        public Task<GetSyncStateAPIResult> GetSyncState()
        {
            throw new NotImplementedException();
        }

        public async Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            var tasks = _primaryClients.Select(async client => await client.Value.GetTokenGenesisBlock(AccountId, TokenTicker, Signature)).ToList();

            return await BlockResultAsync(tasks);
        }

        public Task<GetListStringAPIResult> GetTokenNames(string AccountId, string Signature, string keyword)
        {
            throw new NotImplementedException();
        }

        public Task<List<TransStats>> GetTransStatsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion)
        {
            throw new NotImplementedException();
        }

        public List<Voter> GetVoters(VoteQueryModel model)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<NewFeesAPIResult> LookForNewFees(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<TradeAPIResult> LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> ReceiveFee(ReceiveAuthorizerFeeBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<APIResult> RequestMarket(string tokenName)
        {
            throw new NotImplementedException();
        }

        public Task<TransactionsAPIResult> SearchTransactions(string accountId, long startTimeTicks, long endTimeTicks, int count)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> SendExchangeTransfer(ExchangingBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<CancelKey> SubmitExchangeOrder(TokenTradeOrder order)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> Trade(TradeBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<TradeOrderAuthorizationAPIResult> TradeOrder(TradeOrderBlock block)
        {
            throw new NotImplementedException();
        }
    }
}
