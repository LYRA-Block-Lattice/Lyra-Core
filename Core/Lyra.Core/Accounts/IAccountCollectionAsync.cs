using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.ODR;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
using Lyra.Data.Blocks;
using MongoDB.Bson;
using static Lyra.Core.Accounts.MongoAccountCollection;

namespace Lyra.Core.Accounts
{
    /// <summary>
    /// hole block lists.
    /// </summary>
    public interface IAccountCollectionAsync : IDisposable
    {
        Task UpdateStatsAsync();
        // for service
        Task<long> GetBlockCountAsync();
        Task<long> GetBlockCountAsync(string AccountId);
        //int GetTotalBlockCount();
        Task<bool> AccountExistsAsync(string AccountId);
        Task<Block> FindLatestBlockAsync();
        Task<Block> FindLatestBlockAsync(string AccountId);
        Task<Block> FindFirstBlockAsync(string AccountId);
        TransactionBlock FindFirstBlock(string AccountId);
        Task<TokenGenesisBlock> FindTokenGenesisBlockAsync(string Hash, string Ticker);

        // DAO and OTC
        Task<List<TransactionBlock>> FindAllVotesByDaoAsync(string daoid, bool openOnly);
        Task<List<TransactionBlock>> FindAllVoteForTradeAsync(string tradeid);
        Task<VotingSummary> GetVoteSummaryAsync(string voteid);
        Task<TransactionBlock> FindExecForVoteAsync(string voteid);

        Task<List<DaoGenesisBlock>> FindDaosAsync(string keyword);
        Task<List<TransactionBlock>> GetAllDaosAsync(int page, int pageSize);
        Block GetDaoByName(string name);
        Task<List<Block>> GetOtcOrdersByOwnerAsync(string accountId);
        Task<Dictionary<string, List<TransactionBlock>>> FindTradableOrdersAsync();
        Task<List<TransactionBlock>> FindOtcTradeAsync(string accountId, bool onlyOpenTrade, int page, int pageSize);
        Task<List<TransactionBlock>> FindOtcTradeByStatusAsync(string daoid, OTCTradeStatus status, int page, int pageSize);
        Task<List<TransactionBlock>> FindOtcTradeForOrderAsync(string orderid);
        Task<List<TradeStats>> GetOtcTradeStatsForUsersAsync(List<string> accountIds);

        // Universal Order and trade
        Task<List<TransactionBlock>> GetUniOrdersByOwnerAsync(string accountId);
        Task<Dictionary<string, List<TransactionBlock>>> FindTradableUniAsync();
        Task<List<TransactionBlock>> FindUniTradeAsync(string accountId, bool onlyOpenTrade, int page, int pageSize);
        Task<List<TransactionBlock>> FindUniTradeByStatusAsync(string daoid, UniTradeStatus status, int page, int pageSize);
        Task<List<TransactionBlock>> FindUniTradeForOrderAsync(string orderid);
        Task<List<TradeStats>> GetUniTradeStatsForUsersAsync(List<string> accountIds);

        Block GetDealerByName(string name);
        Block GetDealerByAccountId(string accountId);

        Task<List<TokenGenesisBlock>> FindTokenGenesisBlocksAsync(string keyword);
        Task<List<TokenGenesisBlock>> FindTokensAsync(string keyword, string catalog);
        Task<List<TokenGenesisBlock>?> FindTokensForAccountAsync(string accountId, string keyword, string catalog);
        Block FindBlockByHash(string hash);
        Task<Block> FindBlockByHashAsync(string hash);
        Task<Block> FindBlockByHashAsync(string AccountId, string hash);
        Task<ReceiveTransferBlock> FindBlockBySourceHashAsync(string hash);
        Task<List<NonFungibleToken>> GetNonFungibleTokensAsync(string AccountId);
        Task<List<NonFungibleToken>> GetIssuedNFTInstancesAsync(bool GetOnlySendBlocks, string AccountId, string TokenCode);
        Task<bool> DoesAccountHaveCollectibleNFTInstanceAsync(string owner_account_id, TokenGenesisBlock token_block, string serial_number);
        Task<TransactionBlock> FindBlockByPreviousBlockHashAsync(string previousBlockHash);
        Task<TransactionBlock?> FindBlockByIndexAsync(string AccountId, long index);
        Task<List<TransactionDescription>> SearchTransactionsAsync(string accountId, DateTime startTime, DateTime endTime, int count);
        Task<ServiceBlock> FindServiceBlockByIndexAsync(Int64 index);
        Task<SendTransferBlock> FindUnsettledSendBlockAsync(string AccountId);
        Task<SendTransferBlock> FindUnsettledSendBlockByDestinationAccountIdAsync(string AccountId);
        Task<List<Block>> GetImportedAccountBlocksAsync(string AccountId);
        Task<UnSettledFees> FindUnsettledFeesAsync(string AuthorizerAccountId, string pftid);
        Task<UnSettledFees> FindUnsettledFeesAsync(string AuthorizerAccountId, string pftid, long fromHeight, long endHeight);
        // for service blocks
        Task<ServiceBlock> GetServiceGenesisBlockAsync();
        Task<LyraTokenGenesisBlock> GetLyraTokenGenesisBlockAsync();
        ServiceBlock GetLastServiceBlock();
        Task<ServiceBlock> GetLastServiceBlockAsync();
        Task<ConsolidationBlock> GetLastConsolidationBlockAsync();
        Task<List<ConsolidationBlock>> GetConsolidationBlocksAsync(long startHeight, int count);
        Task<List<ConsolidationBlock>> GetConsolidationBlocksAsync(string belongToSvcHash);

        // NFT related
        Task<SendTransferBlock> FindNFTGenesisSendAsync(string accountId, string ticker, string serial);
        long GetCurrentView();
        /*
        /// <summary>
        /// Returns the first unexecuted trade aimed to an order created on the account.
        /// </summary>
        /// <param name="AccountId"></param>
        /// <param name="BuyTokenCode">
        /// The code of the token being purchased (optional).
        /// </param>
        /// <param name="SellTokenCode">
        /// The code of the token being sold (optional).
        /// </param>
        /// <returns></returns>
        TradeBlock FindUnexecutedTrade(string AccountId, string BuyTokenCode, string SellTokenCode);

        List<TradeOrderBlock> GetTradeOrderBlocks();
        
        Task<List<TradeOrderBlock>> GetSellTradeOrdersForTokenAsync(string BuyTokenCode);
        Task<List<TradeOrderBlock>> GetSellTradeOrdersAsync(string SellTokenCode, string BuyTokenCode);

        List<string> GetTradeOrderCancellations();

        // returns the list of hashes (order IDs) of all cancelled trade order blocks
        List<string> GetExecutedTradeOrderBlocks(); */
        /*
                Task<CancelTradeOrderBlock> GetCancelTradeOrderBlockAsync(string TradeOrderId);
                Task<ExecuteTradeOrderBlock> GetExecuteTradeOrderBlockAsync(string TradeOrderId);
        */
        Task<bool> AddBlockAsync(Block block);
        Task RemoveBlockAsync(string hash);

        Task<List<Block>> GetAllUnConsolidatedBlocksAsync();

        List<Voter> GetVoters(List<string> posAccountIds, DateTime endTime);
        List<Vote> FindVotes(List<string> posAccountIds, DateTime endTime);
        Task<FeeStats> GetFeeStatsAsync();

        Task<List<Block>> GetBlocksByTimeRangeAsync(DateTime startTime, DateTime endTime);
        Task<List<string>> GetBlockHashesByTimeRangeAsync(DateTime startTime, DateTime endTime);
        /// <summary>
        /// Check if this account was ever imported to ANY other account
        /// </summary>
        Task<bool> WasAccountImportedAsync(string ImportedAccountId);

        /// <summary>
        /// Check if the account was  imported to specific account
        /// </summary>
        Task<bool> WasAccountImportedAsync(string ImportedAccountId, string AccountId);

        Task<PoolFactoryBlock> GetPoolFactoryAsync();
        PoolGenesisBlock GetPoolByID(string poolid);
        Task<PoolGenesisBlock> GetPoolAsync(string token0, string token1);
        Task<List<Block>> GetAllBrokerAccountsForOwnerAsync(string ownerAccount);
        Task<List<Block>> FindBlocksByRelatedTxAsync(string hash);
        /// <summary>
        /// Cleans up or deletes blocks collection.
        /// Used for unit testing.
        /// </summary>
        void Delete(bool backup);

        Task<List<Profiting>> FindAllProfitingAccountsAsync(DateTime begin, DateTime end);
        List<Staker> FindAllStakings(string pftid, DateTime timeBefore);
        Task<List<ProfitingGenesis>> FindAllProfitingAccountForOwnerAsync(string ownerAccountId);
        ProfitingGenesis FindProfitingAccountsByName(string Name);
        Task<List<StakingGenesis>> FindAllStakingAccountForOwnerAsync(string ownerAccountId);
        Task<ProfitingStats> GetAccountStatsAsync(string accountId, DateTime begin, DateTime end);
        Task<ProfitingStats> GetBenefitStatsAsync(string pftid, string stkid, DateTime begin, DateTime end);
        Task<decimal> GetPendingReceiveAsync(string accountId);
        Task<PendingStats> GetPendingStatsAsync(string accountId);

        // DEX
        Task<List<TransactionBlock>> GetAllDexWalletsAsync(string owner);
        Task<TransactionBlock?> FindDexWalletAsync(string owner, string symbol, string provider);

        // Fiat
        Task<List<TransactionBlock>> GetAllFiatWalletsAsync(string owner);
        Task<TransactionBlock?> FindFiatWalletAsync(string owner, string symbol);

        Task<List<Dictionary<string, object>>> FindTradableUniOrdersAsync(string? catalog);
        Task<BsonDocument> FindTradableUniOrders2Async(string? catalog);
        Task<List<TransactionBlock>?> GetUniOrderByIdAsync(string orderId);

        // v2
        Task<List<BsonDocument>> GetBalanceAsync(string accountId);
    }
}
