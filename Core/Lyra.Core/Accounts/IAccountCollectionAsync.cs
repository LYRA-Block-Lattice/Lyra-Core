using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.Blocks;

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
        Task<Block> FindFirstBlockAsync(string AccountId);
        TransactionBlock FindFirstBlock(string AccountId);
        Task<TokenGenesisBlock> FindTokenGenesisBlockAsync(string Hash, string Ticker);
        Task<List<TokenGenesisBlock>> FindTokenGenesisBlocksAsync(string keyword);
        Block FindBlockByHash(string hash);
        Task<Block> FindBlockByHashAsync(string hash);
        Task<Block> FindBlockByHashAsync(string AccountId, string hash);
        Task<ReceiveTransferBlock> FindBlockBySourceHashAsync(string hash);
        Task<List<NonFungibleToken>> GetNonFungibleTokensAsync(string AccountId);
        Task<List<NonFungibleToken>> GetIssuedNFTInstancesAsync(bool GetOnlySendBlocks, string AccountId, string TokenCode);
        Task<bool> DoesAccountHaveCollectibleNFTInstanceAsync(string owner_account_id, TokenGenesisBlock token_block, string serial_number);
        Task<TransactionBlock> FindBlockByPreviousBlockHashAsync(string previousBlockHash);
        Task<TransactionBlock> FindBlockByIndexAsync(string AccountId, Int64 index);
        Task<List<TransactionDescription>> SearchTransactionsAsync(string accountId, DateTime startTime, DateTime endTime, int count);
        Task<ServiceBlock> FindServiceBlockByIndexAsync(Int64 index);
        Task<SendTransferBlock> FindUnsettledSendBlockAsync(string AccountId);
        Task<SendTransferBlock> FindUnsettledSendBlockByDestinationAccountIdAsync(string AccountId);
        Task<List<Block>> GetImportedAccountBlocksAsync(string AccountId);
        Task<UnSettledFees> FindUnsettledFeesAsync(string AuthorizerAccountId);
        Task<UnSettledFees> FindUnsettledFeesAsync(string AuthorizerAccountId, long fromHeight, long endHeight);
        // for service blocks
        Task<ServiceBlock> GetServiceGenesisBlockAsync();
        Task<LyraTokenGenesisBlock> GetLyraTokenGenesisBlockAsync();
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
        long GetCurrentView();
        Task<List<TradeOrderBlock>> GetSellTradeOrdersForTokenAsync(string BuyTokenCode);
        Task<List<TradeOrderBlock>> GetSellTradeOrdersAsync(string SellTokenCode, string BuyTokenCode);

        List<string> GetTradeOrderCancellations();

        // returns the list of hashes (order IDs) of all cancelled trade order blocks
        List<string> GetExecutedTradeOrderBlocks();

        Task<CancelTradeOrderBlock> GetCancelTradeOrderBlockAsync(string TradeOrderId);
        Task<ExecuteTradeOrderBlock> GetExecuteTradeOrderBlockAsync(string TradeOrderId);

        Task<bool> AddBlockAsync(Block block);
        Task RemoveBlockAsync(string hash);

        Task<List<Block>> GetAllUnConsolidatedBlocksAsync();

        List<Voter> GetVoters(List<string> posAccountIds, DateTime endTime);
        List<Vote> FindVotes(List<string> posAccountIds, DateTime endTime);
        FeeStats GetFeeStats();

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
        Task<PoolGenesisBlock> GetPoolAsync(string token0, string token1);
        Task<List<Block>> FindBlocksByRelatedTxAsync(string hash);
        /// <summary>
        /// Cleans up or deletes blocks collection.
        /// Used for unit testing.
        /// </summary>
        void Delete(bool backup);
        void CreateBlueprint(BrokerBlueprint blueprint);
        BrokerBlueprint GetBlueprint(string relatedTx);
        void RemoveBlueprint(string hash);
        List<BrokerBlueprint> GetAllBlueprints();
    }
}
