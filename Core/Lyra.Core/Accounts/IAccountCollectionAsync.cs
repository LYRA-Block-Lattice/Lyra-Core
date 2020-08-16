using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Authorizers;
using Lyra.Core.Blocks;

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
        Task<NullTransactionBlock> FindNullTransBlockByHashAsync(string hash);
        Task<Block> FindBlockByHashAsync(string hash);
        Task<Block> FindBlockByHashAsync(string AccountId, string hash);
        Task<ReceiveTransferBlock> FindBlockBySourceHashAsync(string hash);
        Task<List<NonFungibleToken>> GetNonFungibleTokensAsync(string AccountId);
        Task<TransactionBlock> FindBlockByPreviousBlockHashAsync(string previousBlockHash);
        Task<TransactionBlock> FindBlockByIndexAsync(string AccountId, Int64 index);
        Task<Block> FindServiceBlockByIndexAsync(string blockType, Int64 index);
        Task<SendTransferBlock> FindUnsettledSendBlockAsync(string AccountId);
        Task<IEnumerable<ServiceBlock>> FindUnsettledFeeBlockAsync(string AuthorizerAccountId);

        // for service blocks
        Task<ServiceBlock> GetServiceGenesisBlock();
        Task<LyraTokenGenesisBlock> GetLyraTokenGenesisBlock();
        Task<ServiceBlock> GetLastServiceBlockAsync();
        Task<ConsolidationBlock> GetLastConsolidationBlockAsync();
        Task<List<ConsolidationBlock>> GetConsolidationBlocksAsync(long startHeight);
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

        Task<bool> ConsolidateBlock(string hash);
        Task<IEnumerable<Block>> GetAllUnConsolidatedBlocksAsync();
        Task<IEnumerable<string>> GetAllUnConsolidatedBlockHashesAsync();

        //Task<Vote> GetVotesForAccountAsync(string accountId);
        //Task UpdateVotesForAccountAsync(Vote vote);

        List<Vote> FindVotes(IEnumerable<string> posAccountIds);
        Task<IEnumerable<string>> GetBlockHashesByTimeRange(DateTime startTime, DateTime endTime);

        /// <summary>
        /// Cleans up or deletes blocks collection.
        /// Used for unit testing.
        /// </summary>
        void Delete();
    }
}
