using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyra.Core.Blocks;
using Lyra.Data.API;

namespace Lyra.Core.Accounts
{
    /// <summary>
    /// hole block lists.
    /// </summary>
    public interface IAccountCollectionAsync : IDisposable
    {
        // for service
        Task<long> GetBlockCountAsync();
        Task<long> GetBlockCountAsync(string AccountId);
        //int GetTotalBlockCount();
        Task<bool> AccountExistsAsync(string AccountId);
        Task<Block> FindLatestBlockAsync();
        Task<Block> FindLatestBlockAsync(string AccountId);
        Task<TokenGenesisBlock> FindTokenGenesisBlockAsync(string Hash, string Ticker);
        Task<List<TokenGenesisBlock>> FindTokenGenesisBlocksAsync(string keyword);
        Task<Block> FindBlockByHashAsync(string hash);
        Task<Block> FindBlockByHashAsync(string AccountId, string hash);
        Task<ReceiveTransferBlock> FindBlockBySourceHashAsync(string hash);
        Task<List<NonFungibleToken>> GetNonFungibleTokensAsync(string AccountId);
        Task<List<NonFungibleToken>> GetIssuedNFTInstancesAsync(bool GetOnlySendBlocks, string AccountId, string TokenCode);
        Task<bool> DoesAccountHaveCollectibleNFTInstanceAsync(string owner_account_id, TokenGenesisBlock token_block, string serial_number);
        Task<TransactionBlock> FindBlockByPreviousBlockHashAsync(string previousBlockHash);
        Task<TransactionBlock> FindBlockByIndexAsync(string AccountId, Int64 index);
        Task<List<TransactionDescription>> SearchTransactions(string accountId, DateTime startTime, DateTime endTime, int count);
        Task<ServiceBlock> FindServiceBlockByIndexAsync(Int64 index);
        Task<SendTransferBlock> FindUnsettledSendBlockAsync(string AccountId);
        Task<SendTransferBlock> FindUnsettledSendBlockByDestinationAccountIdAsync(string AccountId);
        Task<List<Block>> GetImportedAccountBlocksAsync(string AccountId);
        Task<UnSettledFees> FindUnsettledFeesAsync(string AuthorizerAccountId);
        Task<UnSettledFees> FindUnsettledFeesAsync(string AuthorizerAccountId, long fromHeight, long endHeight);
        // for service blocks
        Task<ServiceBlock> GetServiceGenesisBlock();
        Task<LyraTokenGenesisBlock> GetLyraTokenGenesisBlock();
        Task<ServiceBlock> GetLastServiceBlockAsync();
        Task<ConsolidationBlock> GetLastConsolidationBlockAsync();
        Task<List<ConsolidationBlock>> GetConsolidationBlocksAsync(long startHeight, int count);
        Task<List<ConsolidationBlock>> GetConsolidationBlocksAsync(string belongToSvcHash);

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
        Task<List<TradeOrderBlock>> GetSellTradeOrdersForToken(string BuyTokenCode);
        Task<List<TradeOrderBlock>> GetSellTradeOrders(string SellTokenCode, string BuyTokenCode);

        List<string> GetTradeOrderCancellations();

        // returns the list of hashes (order IDs) of all cancelled trade order blocks
        List<string> GetExecutedTradeOrderBlocks();

        Task<CancelTradeOrderBlock> GetCancelTradeOrderBlock(string TradeOrderId);
        Task<ExecuteTradeOrderBlock> GetExecuteTradeOrderBlock(string TradeOrderId);

        Task<bool> AddBlockAsync(Block block);
        Task RemoveBlockAsync(string hash);

        Task<IEnumerable<Block>> GetAllUnConsolidatedBlocksAsync();

        List<Voter> GetVoters(List<string> posAccountIds, DateTime endTime);
        List<Vote> FindVotes(List<string> posAccountIds, DateTime endTime);
        FeeStats GetFeeStats();

        Task<List<Block>> GetBlocksByTimeRange(DateTime startTime, DateTime endTime);
        Task<IEnumerable<string>> GetBlockHashesByTimeRange(DateTime startTime, DateTime endTime);
        /// <summary>
        /// Check if this account was ever imported to ANY other account
        /// </summary>
        Task<bool> WasAccountImportedAsync(string ImportedAccountId);

        /// <summary>
        /// Check if the account was  imported to specific account
        /// </summary>
        Task<bool> WasAccountImportedAsync(string ImportedAccountId, string AccountId);

        Task<PoolFactoryBlock> GetPoolFactoryAsync();
        Task<PoolGenesisBlock> GetPoolAsync(string token0, string token1);
        Task<TransactionBlock> GetPoolByAccountIdAsync(string poolAccountId);
        /// <summary>
        /// Cleans up or deletes blocks collection.
        /// Used for unit testing.
        /// </summary>
        void Delete();
    }
}
