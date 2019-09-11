using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;

namespace Lyra.Core.Accounts.Node
{
    public interface IAccountCollection: IDisposable
    {
        long GetBlockCount(string AccountId);
        //int GetTotalBlockCount();
        bool AccountExists(string AccountId);
        TransactionBlock FindLatestBlock(string AccountId);
        TokenGenesisBlock FindTokenGenesisBlock(string Hash, string Ticker);
        TransactionBlock FindBlockByHash(string hash);
        TransactionBlock FindBlockByHash(string AccountId, string hash);
        ReceiveTransferBlock FindBlockBySourceHash(string hash);
        List<NonFungibleToken> GetNonFungibleTokens(string AccountId);
        TransactionBlock FindBlockByPreviousBlockHash(string previousBlockHash);
        TransactionBlock FindBlockByIndex(string AccountId, int index);
        SendTransferBlock FindUnsettledSendBlock(string AccountId);

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

        List<string> GetTradeOrderCancellations();

        // returns the list of hashes (order IDs) of all cancelled trade order blocks
        List<string> GetExecutedTradeOrderBlocks();

        void AddBlock(TransactionBlock block);

        /// <summary>
        /// Cleans up or deletes blocks collection.
        /// Used for unit testing.
        /// </summary>
        void Delete();
    }
}
