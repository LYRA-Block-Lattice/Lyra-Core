using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.ODR;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
using Lyra.Data.Blocks;
using Lyra.Shared;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Lyra.Core.Accounts.MongoAccountCollection;

namespace Lyra.Core.Accounts
{
    public class TracedStorage : IAccountCollectionAsync
    {
        IAccountCollectionAsync _store;
        public TracedStorage(IAccountCollectionAsync store)
        {
            _store = store;
        }

        public IAccountCollectionAsync Store => _store;
        public async Task<List<Block>> GetAllUnConsolidatedBlocksAsync() => await StopWatcher.TrackAsync(() => _store.GetAllUnConsolidatedBlocksAsync(), StopWatcher.GetCurrentMethod());
        public async Task<ConsolidationBlock> GetLastConsolidationBlockAsync() => await StopWatcher.TrackAsync(() => _store.GetLastConsolidationBlockAsync(), StopWatcher.GetCurrentMethod());//_store.GetSyncBlockAsync();
        public async Task<List<ConsolidationBlock>> GetConsolidationBlocksAsync(long startHeight, int count) => await StopWatcher.TrackAsync(() => _store.GetConsolidationBlocksAsync(startHeight, count), StopWatcher.GetCurrentMethod());
        public async Task<ServiceBlock> GetLastServiceBlockAsync() => await StopWatcher.TrackAsync(() => _store.GetLastServiceBlockAsync(), StopWatcher.GetCurrentMethod());//_store.GetLastServiceBlockAsync();

        // forward api. should have more control here.
        public async Task<bool> AddBlockAsync(Block block) => await StopWatcher.TrackAsync(() => _store.AddBlockAsync(block), StopWatcher.GetCurrentMethod());
        public async Task RemoveBlockAsync(string hash) => await _store.RemoveBlockAsync(hash);
        //public async Task AddBlockAsync(ServiceBlock serviceBlock) => await StopWatcher.Track(_store.AddBlockAsync(serviceBlock), StopWatcher.GetCurrentMethod());//_store.AddBlockAsync(serviceBlock);

        // bellow readonly access
        //public async Task<Block> FindBlockByHeightAsync(string AccountId, long height) => await StopWatcher.TrackAsync(() => _store.FindBlockByHeightAsync(AccountId, height), StopWatcher.GetCurrentMethod());//_store.FindLatestBlockAsync(AccountId);
        public async Task<bool> AccountExistsAsync(string AccountId) => await StopWatcher.TrackAsync(() => _store.AccountExistsAsync(AccountId), StopWatcher.GetCurrentMethod());//_store.AccountExistsAsync(AccountId);
        public async Task<Block> FindLatestBlockAsync() => await StopWatcher.TrackAsync(() => _store.FindLatestBlockAsync(), StopWatcher.GetCurrentMethod());//_store.FindLatestBlockAsync();
        public async Task<Block> FindLatestBlockAsync(string AccountId) => await StopWatcher.TrackAsync(() => _store.FindLatestBlockAsync(AccountId), StopWatcher.GetCurrentMethod());//_store.FindLatestBlockAsync(AccountId);
        public async Task<Block> FindBlockByHashAsync(string hash) => await StopWatcher.TrackAsync(() => _store.FindBlockByHashAsync(hash), StopWatcher.GetCurrentMethod());//_store.FindBlockByHashAsync(hash);
        public async Task<Block> FindBlockByHashAsync(string AccountId, string hash) => await StopWatcher.TrackAsync(() => _store.FindBlockByHashAsync(AccountId, hash), StopWatcher.GetCurrentMethod());//_store.FindBlockByHashAsync(AccountId, hash);
        public async Task<List<TokenGenesisBlock>> FindTokenGenesisBlocksAsync(string keyword) => await StopWatcher.TrackAsync(() => _store.FindTokenGenesisBlocksAsync(keyword), StopWatcher.GetCurrentMethod());//_store.FindTokenGenesisBlocksAsync(keyword);
        public async Task<List<TokenGenesisBlock>> FindTokensForAccountAsync(string accountId, string keyword, string catalog) => await StopWatcher.TrackAsync(() => _store.FindTokensForAccountAsync(accountId, keyword, catalog), StopWatcher.GetCurrentMethod());//_store.FindTokensAsync(keyword);
        public async Task<List<TokenGenesisBlock>> FindTokensAsync(string keyword, string catalog) => await StopWatcher.TrackAsync(() => _store.FindTokensAsync(keyword, catalog), StopWatcher.GetCurrentMethod());//_store.FindTokensAsync(keyword);
        public async Task<List<DaoGenesisBlock>> FindDaosAsync(string keyword) => await StopWatcher.TrackAsync(() => _store.FindDaosAsync(keyword), StopWatcher.GetCurrentMethod());//_store.FindDaosAsync(keyword);
        public async Task<TokenGenesisBlock> FindTokenGenesisBlockAsync(string Hash, string Ticker) => await StopWatcher.TrackAsync(() => _store.FindTokenGenesisBlockAsync(Hash, Ticker), StopWatcher.GetCurrentMethod());//_store.FindTokenGenesisBlockAsync(Hash, Ticker);
        public async Task<ReceiveTransferBlock> FindBlockBySourceHashAsync(string hash) => await StopWatcher.TrackAsync(() => _store.FindBlockBySourceHashAsync(hash), StopWatcher.GetCurrentMethod());//_store.FindBlockBySourceHashAsync(hash);
        public async Task<long> GetBlockCountAsync() => await StopWatcher.TrackAsync(() => _store.GetBlockCountAsync(), StopWatcher.GetCurrentMethod());//_store.GetBlockCountAsync();
        public async Task<TransactionBlock> FindBlockByIndexAsync(string AccountId, long index) => await StopWatcher.TrackAsync(() => _store.FindBlockByIndexAsync(AccountId, index), StopWatcher.GetCurrentMethod());//_store.FindBlockByIndexAsync(AccountId, index);
        public async Task<List<NonFungibleToken>> GetNonFungibleTokensAsync(string AccountId) => await StopWatcher.TrackAsync(() => _store.GetNonFungibleTokensAsync(AccountId), StopWatcher.GetCurrentMethod());//_store.GetNonFungibleTokensAsync(AccountId);
        public async Task<SendTransferBlock> FindUnsettledSendBlockAsync(string AccountId) => await StopWatcher.TrackAsync(() => _store.FindUnsettledSendBlockAsync(AccountId), StopWatcher.GetCurrentMethod());//_store.FindUnsettledSendBlockAsync(AccountId);
        public async Task<TransactionBlock> FindBlockByPreviousBlockHashAsync(string previousBlockHash) => await StopWatcher.TrackAsync(() => _store.FindBlockByPreviousBlockHashAsync(previousBlockHash), StopWatcher.GetCurrentMethod());//_store.FindBlockByPreviousBlockHashAsync(previousBlockHash);
        public async Task<Block> FindBlockByHeightAsync(string AccountId, long height) => await StopWatcher.TrackAsync(() => _store.FindBlockByHeightAsync(AccountId, height), StopWatcher.GetCurrentMethod());//_store.FindBlockByHeightAsync(AccountId, height);
        // v2
        public async Task<List<Dictionary<string, object>>> FindTradableUniOrdersAsync(string? catalog) => await StopWatcher.TrackAsync(() => _store.FindTradableUniOrdersAsync(catalog), StopWatcher.GetCurrentMethod());
        public async Task<BsonDocument> FindTradableUniOrders2Async(string? catalog) => await StopWatcher.TrackAsync(() => _store.FindTradableUniOrders2Async(catalog), StopWatcher.GetCurrentMethod());
        public async Task<List<TransactionBlock>?> GetUniOrderByIdAsync(string orderId) => await StopWatcher.TrackAsync(() => _store.GetUniOrderByIdAsync(orderId), StopWatcher.GetCurrentMethod());

        public Task UpdateStatsAsync()
        {
            return _store.UpdateStatsAsync();
        }

        public Task<long> GetBlockCountAsync(string AccountId)
        {
            return StopWatcher.TrackAsync(() => _store.GetBlockCountAsync(AccountId), StopWatcher.GetCurrentMethod());
        }

        public Task<Block> FindFirstBlockAsync(string AccountId)
        {
            return StopWatcher.TrackAsync(() => _store.FindFirstBlockAsync(AccountId), StopWatcher.GetCurrentMethod());
        }

        public TransactionBlock FindFirstBlock(string AccountId)
        {
            return StopWatcher.Track(() => _store.FindFirstBlock(AccountId), "FindFirstBlock");
        }

        public Block FindBlockByHash(string hash)
        {
            return StopWatcher.Track(() => _store.FindBlockByHash(hash), "FindBlockByHash");
        }

        public Task<List<NonFungibleToken>> GetIssuedNFTInstancesAsync(bool GetOnlySendBlocks, string AccountId, string TokenCode)
        {
            return StopWatcher.TrackAsync(() => _store.GetIssuedNFTInstancesAsync(GetOnlySendBlocks, AccountId, TokenCode), StopWatcher.GetCurrentMethod());
        }

        public Task<bool> DoesAccountHaveCollectibleNFTInstanceAsync(string owner_account_id, TokenGenesisBlock token_block, string serial_number)
        {
            return StopWatcher.TrackAsync(() => _store.DoesAccountHaveCollectibleNFTInstanceAsync(owner_account_id, token_block, serial_number), StopWatcher.GetCurrentMethod());
        }

        public Task<List<TransactionDescription>> SearchTransactionsAsync(string accountId, DateTime startTime, DateTime endTime, int count)
        {
            return StopWatcher.TrackAsync(() => _store.SearchTransactionsAsync(accountId, startTime, endTime, count), StopWatcher.GetCurrentMethod());
        }

        public Task<ServiceBlock> FindServiceBlockByIndexAsync(long index)
        {
            return StopWatcher.TrackAsync(() => _store.FindServiceBlockByIndexAsync(index), StopWatcher.GetCurrentMethod());
        }

        public Task<SendTransferBlock> FindUnsettledSendBlockByDestinationAccountIdAsync(string AccountId)
        {
            return StopWatcher.TrackAsync(() => _store.FindUnsettledSendBlockByDestinationAccountIdAsync(AccountId), StopWatcher.GetCurrentMethod());
        }

        public Task<List<Block>> GetImportedAccountBlocksAsync(string AccountId)
        {
            return StopWatcher.TrackAsync(() => _store.GetImportedAccountBlocksAsync(AccountId), StopWatcher.GetCurrentMethod());
        }

        public Task<UnSettledFees> FindUnsettledFeesAsync(string AuthorizerAccountId, string pftid)
        {
            return StopWatcher.TrackAsync(() => _store.FindUnsettledFeesAsync(AuthorizerAccountId, pftid), StopWatcher.GetCurrentMethod());
        }

        public Task<UnSettledFees> FindUnsettledFeesAsync(string AuthorizerAccountId, string pftid, long fromHeight, long endHeight)
        {
            return StopWatcher.TrackAsync(() => _store.FindUnsettledFeesAsync(AuthorizerAccountId, pftid, fromHeight, endHeight), StopWatcher.GetCurrentMethod());
        }

        public Task<ServiceBlock> GetServiceGenesisBlockAsync()
        {
            return StopWatcher.TrackAsync(() => _store.GetServiceGenesisBlockAsync(), StopWatcher.GetCurrentMethod());
        }

        public Task<LyraTokenGenesisBlock> GetLyraTokenGenesisBlockAsync()
        {
            return StopWatcher.TrackAsync(() => _store.GetLyraTokenGenesisBlockAsync(), StopWatcher.GetCurrentMethod());
        }

        public ServiceBlock GetLastServiceBlock()
        {
            return StopWatcher.Track(() => _store.GetLastServiceBlock(), StopWatcher.GetCurrentMethod());
        }

        public Task<List<ConsolidationBlock>> GetConsolidationBlocksAsync(string belongToSvcHash)
        {
            return StopWatcher.TrackAsync(() => _store.GetConsolidationBlocksAsync(belongToSvcHash), StopWatcher.GetCurrentMethod());
        }

        public long GetCurrentView()
        {
            return StopWatcher.Track(() => _store.GetCurrentView(), "GetCurrentView");
        }
        /*
        public TradeBlock FindUnexecutedTrade(string AccountId, string BuyTokenCode, string SellTokenCode)
        {
            return StopWatcher.Track(() => _store.FindUnexecutedTrade(AccountId, BuyTokenCode, SellTokenCode), "FindUnexecutedTrade");
        }

        public List<TradeOrderBlock> GetTradeOrderBlocks()
        {
            return StopWatcher.Track(() => _store.GetTradeOrderBlocks(), "GetTradeOrderBlocks");
        }

        public Task<List<TradeOrderBlock>> GetSellTradeOrdersForTokenAsync(string BuyTokenCode)
        {
            return StopWatcher.TrackAsync(() => _store.GetSellTradeOrdersForTokenAsync(BuyTokenCode), StopWatcher.GetCurrentMethod());
        }

        public Task<List<TradeOrderBlock>> GetSellTradeOrdersAsync(string SellTokenCode, string BuyTokenCode)
        {
            return StopWatcher.TrackAsync(() => _store.GetSellTradeOrdersAsync(SellTokenCode, BuyTokenCode), StopWatcher.GetCurrentMethod());
        }

        public List<string> GetTradeOrderCancellations()
        {
            return StopWatcher.Track(() => _store.GetTradeOrderCancellations(), "GetTradeOrderCancellations");
        }

        public List<string> GetExecutedTradeOrderBlocks()
        {
            return StopWatcher.Track(() => _store.GetExecutedTradeOrderBlocks(), "GetExecutedTradeOrderBlocks");
        }
        
        public Task<CancelTradeOrderBlock> GetCancelTradeOrderBlockAsync(string TradeOrderId)
        {
            return StopWatcher.TrackAsync(() => _store.GetCancelTradeOrderBlockAsync(TradeOrderId), StopWatcher.GetCurrentMethod());
        }

        public Task<ExecuteTradeOrderBlock> GetExecuteTradeOrderBlockAsync(string TradeOrderId)
        {
            return StopWatcher.TrackAsync(() => _store.GetExecuteTradeOrderBlockAsync(TradeOrderId), StopWatcher.GetCurrentMethod());
        }*/

        public List<Voter> GetVoters(List<string> posAccountIds, DateTime endTime)
        {
            return StopWatcher.Track(() => _store.GetVoters(posAccountIds, endTime), "GetVoters");
        }

        public List<Vote> FindVotes(List<string> posAccountIds, DateTime endTime)
        {
            return StopWatcher.Track(() => _store.FindVotes(posAccountIds, endTime), "FindVotes");
        }

        public Task<FeeStats> GetFeeStatsAsync()
        {
            return StopWatcher.Track(() => _store.GetFeeStatsAsync(), "GetFeeStatsAsync");
        }

        public Task<List<Block>> GetBlocksByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            return StopWatcher.TrackAsync(() => _store.GetBlocksByTimeRangeAsync(startTime, endTime), StopWatcher.GetCurrentMethod());
        }

        public Task<List<string>> GetBlockHashesByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            return StopWatcher.TrackAsync(() => _store.GetBlockHashesByTimeRangeAsync(startTime, endTime), StopWatcher.GetCurrentMethod());
        }

        public Task<bool> WasAccountImportedAsync(string ImportedAccountId)
        {
            return StopWatcher.TrackAsync(() => _store.WasAccountImportedAsync(ImportedAccountId), StopWatcher.GetCurrentMethod());
        }

        public Task<bool> WasAccountImportedAsync(string ImportedAccountId, string AccountId)
        {
            return StopWatcher.TrackAsync(() => _store.WasAccountImportedAsync(ImportedAccountId, AccountId), StopWatcher.GetCurrentMethod());
        }

        public Task<PoolFactoryBlock> GetPoolFactoryAsync()
        {
            return StopWatcher.TrackAsync(() => _store.GetPoolFactoryAsync(), StopWatcher.GetCurrentMethod());
        }

        public PoolGenesisBlock GetPoolByID(string poolid)
        {
            return StopWatcher.Track(() => _store.GetPoolByID(poolid), "GetPoolByID");
        }

        public Task<PoolGenesisBlock> GetPoolAsync(string token0, string token1)
        {
            return StopWatcher.TrackAsync(() => _store.GetPoolAsync(token0, token1), StopWatcher.GetCurrentMethod());
        }

        public Task<List<Block>> GetAllBrokerAccountsForOwnerAsync(string ownerAccount)
        {
            return StopWatcher.TrackAsync(() => _store.GetAllBrokerAccountsForOwnerAsync(ownerAccount), StopWatcher.GetCurrentMethod());
        }

        public Task<List<Block>> FindBlocksByRelatedTxAsync(string hash)
        {
            return StopWatcher.TrackAsync(() => _store.FindBlocksByRelatedTxAsync(hash), StopWatcher.GetCurrentMethod());
        }

        public void Delete(bool backup)
        {
            _store.Delete(backup);
        }

        public Task<List<Profiting>> FindAllProfitingAccountsAsync(DateTime begin, DateTime end)
        {
            return StopWatcher.TrackAsync(() => _store.FindAllProfitingAccountsAsync(begin, end), StopWatcher.GetCurrentMethod());
        }

        public List<Staker> FindAllStakings(string pftid, DateTime timeBefore)
        {
            return StopWatcher.Track(() => _store.FindAllStakings(pftid, timeBefore), "FindAllStakings");
        }

        public Task<List<ProfitingGenesis>> FindAllProfitingAccountForOwnerAsync(string ownerAccountId)
        {
            return StopWatcher.TrackAsync(() => _store.FindAllProfitingAccountForOwnerAsync(ownerAccountId), StopWatcher.GetCurrentMethod());
        }

        public ProfitingGenesis FindProfitingAccountsByName(string Name)
        {
            return StopWatcher.Track(() => _store.FindProfitingAccountsByName(Name), "FindProfitingAccountsByName");
        }

        public Task<List<StakingGenesis>> FindAllStakingAccountForOwnerAsync(string ownerAccountId)
        {
            return StopWatcher.TrackAsync(() => _store.FindAllStakingAccountForOwnerAsync(ownerAccountId), StopWatcher.GetCurrentMethod());
        }

        public Task<ProfitingStats> GetAccountStatsAsync(string accountId, DateTime begin, DateTime end)
        {
            return StopWatcher.TrackAsync(() => _store.GetAccountStatsAsync(accountId, begin, end), StopWatcher.GetCurrentMethod());
        }

        public Task<ProfitingStats> GetBenefitStatsAsync(string pftid, string stkid, DateTime begin, DateTime end)
        {
            return StopWatcher.TrackAsync(() => _store.GetBenefitStatsAsync(pftid, stkid, begin, end), StopWatcher.GetCurrentMethod());
        }

        public Task<decimal> GetPendingReceiveAsync(string accountId)
        {
            return StopWatcher.TrackAsync(() => _store.GetPendingReceiveAsync(accountId), StopWatcher.GetCurrentMethod());
        }

        public Task<PendingStats> GetPendingStatsAsync(string accountId)
        {
            return StopWatcher.TrackAsync(() => _store.GetPendingStatsAsync(accountId), StopWatcher.GetCurrentMethod());
        }

        public Task<List<TransactionBlock>> GetAllDexWalletsAsync(string owner)
        {
            return StopWatcher.TrackAsync(() => _store.GetAllDexWalletsAsync(owner), StopWatcher.GetCurrentMethod());
        }

        public Task<TransactionBlock?> FindDexWalletAsync(string owner, string symbol, string provider)
        {
            return StopWatcher.TrackAsync(() => _store.FindDexWalletAsync(owner, symbol, provider), StopWatcher.GetCurrentMethod());
        }

        public Task<List<TransactionBlock>> GetAllFiatWalletsAsync(string owner)
        {
            return StopWatcher.TrackAsync(() => _store.GetAllFiatWalletsAsync(owner), StopWatcher.GetCurrentMethod());
        }

        public Task<TransactionBlock?> FindFiatWalletAsync(string owner, string symbol)
        {
            return StopWatcher.TrackAsync(() => _store.FindFiatWalletAsync(owner, symbol), StopWatcher.GetCurrentMethod());
        }

        public void Dispose()
        {
            _store.Dispose();
        }

        public Task<List<TransactionBlock>> GetAllDaosAsync(int page, int pageSize)
        {
            return StopWatcher.Track(() => _store.GetAllDaosAsync(page, pageSize), "GetAllDaosAsync");
        }

        public Block GetDaoByName(string name)
        {
            return StopWatcher.Track(() => _store.GetDaoByName(name), "GetDaoByName");
        }

        public Block GetDealerByName(string name)
        {
            return StopWatcher.Track(() => _store.GetDealerByName(name), "GetDealerByName");
        }

        public Block GetDealerByAccountId(string accountId)
        {
            return StopWatcher.Track(() => _store.GetDealerByAccountId(accountId), "GetDealerByAccountId");
        }

        public Task<List<TransactionBlock>> FindAllVotesByDaoAsync(string daoid, bool openOnly)
        {
            return StopWatcher.Track(() => _store.FindAllVotesByDaoAsync(daoid, openOnly), "FindAllVotesByDaoAsync");
        }

        public Task<List<TransactionBlock>> FindAllVoteForTradeAsync(string tradeid)
        {
            return StopWatcher.Track(() => _store.FindAllVoteForTradeAsync(tradeid), "FindAllVoteForTradeAsync");
        }

        public Task<VotingSummary> GetVoteSummaryAsync(string voteid)
        {
            return StopWatcher.Track(() => _store.GetVoteSummaryAsync(voteid), "GetVoteSummaryAsync");
        }

        public Task<TransactionBlock> FindExecForVoteAsync(string voteid)
        {
            return StopWatcher.Track(() => _store.FindExecForVoteAsync(voteid), "FindExecForVoteAsync");
        }

        public Task<List<Block>> GetOtcOrdersByOwnerAsync(string accountId)
        {
            return StopWatcher.Track(() => _store.GetOtcOrdersByOwnerAsync(accountId), "GetOtcOrdersByOwner");
        }

        public Task<Dictionary<string, List<TransactionBlock>>> FindTradableOrdersAsync()
        {
            return StopWatcher.Track(() => _store.FindTradableOrdersAsync(), "FindTradableOrdersAsync");
        }

        public Task<List<TransactionBlock>> FindOtcTradeAsync(string accountId, bool onlyOpenTrade, int page, int pageSize)
        {
            return StopWatcher.Track(() => _store.FindOtcTradeAsync(accountId, onlyOpenTrade, page, pageSize), "FindOtcTradeAsync");
        }

        public Task<List<TransactionBlock>> FindOtcTradeByStatusAsync(string daoid, OTCTradeStatus status, int page, int pageSize)
        {
            return StopWatcher.Track(() => _store.FindOtcTradeByStatusAsync(daoid, status, page, pageSize), "FindOtcTradeByStatus");
        }

        public Task<List<TransactionBlock>> FindOtcTradeForOrderAsync(string orderid)
        {
            return StopWatcher.Track(() => _store.FindOtcTradeForOrderAsync(orderid), "FindOtcTradeForOrder");
        }

        public Task<List<TradeStats>> GetOtcTradeStatsForUsersAsync(List<string> accountIds)
        {
            return StopWatcher.Track(() => _store.GetOtcTradeStatsForUsersAsync(accountIds), "GetOtcTradeStatsForUsers");
        }

        public Task<SendTransferBlock> FindNFTGenesisSendAsync(string accountId, string ticker, string serial)
        {
            return StopWatcher.Track(() => _store.FindNFTGenesisSendAsync(accountId, ticker, serial), "FindNFTGenesisSendAsync");
        }

        public Task<List<BsonDocument>> GetBalanceAsync(string accountId)
        {
            return StopWatcher.Track(() => _store.GetBalanceAsync(accountId), "GetBalanceAsync");
        }

        public Task<Block> FindLatestBlockByTimeAsync(string accountId, DateTime time)
        {
            return StopWatcher.Track(() => _store.FindLatestBlockByTimeAsync(accountId, time), "FindLatestBlockByTimeAsync");
        }

        #region Universal trade
        public Task<List<TransactionBlock>> GetUniOrdersByOwnerAsync(string accountId)
        {
            return StopWatcher.Track(() => _store.GetUniOrdersByOwnerAsync(accountId), "GetUniOrdersByOwner");
        }

        public Task<Dictionary<string, List<TransactionBlock>>> FindTradableUniAsync()
        {
            return StopWatcher.Track(() => _store.FindTradableUniAsync(), "FindTradableUniAsync");
        }

        public Task<List<TransactionBlock>> FindUniTradeAsync(string accountId, bool onlyOpenTrade, int page, int pageSize)
        {
            return StopWatcher.Track(() => _store.FindUniTradeAsync(accountId, onlyOpenTrade, page, pageSize), "FindUniTradeAsync");
        }

        public Task<List<TransactionBlock>> FindUniTradeByStatusAsync(string daoid, UniTradeStatus status, int page, int pageSize)
        {
            return StopWatcher.Track(() => _store.FindUniTradeByStatusAsync(daoid, status, page, pageSize), "FindUniTradeByStatus");
        }

        public Task<List<TransactionBlock>> FindUniTradeForOrderAsync(string orderid)
        {
            return StopWatcher.Track(() => _store.FindUniTradeForOrderAsync(orderid), "FindUniTradeForOrder");
        }

        public Task<List<TradeStats>> GetUniTradeStatsForUsersAsync(List<string> accountIds)
        {
            return StopWatcher.Track(() => _store.GetUniTradeStatsForUsersAsync(accountIds), "GetUniTradeStatsForUsers");
        }

        #endregion
    }
}
