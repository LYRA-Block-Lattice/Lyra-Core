﻿using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lyra.Core.API
{
    public interface INodeAPI
    {
        #region Blocklist information methods
        Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion);

        Task<BlockAPIResult> GetServiceGenesisBlock();
        Task<BlockAPIResult> GetLyraTokenGenesisBlock();

        Task<GetSyncStateAPIResult> GetSyncState();

        // this one can be cached for a few milliseconds
        Task<AccountHeightAPIResult> GetSyncHeight();

        Task<GetListStringAPIResult> GetTokenNames(string AccountId, string Signature, string keyword);

        // this one can be cached for a few seconds
        Task<BlockAPIResult> GetLastServiceBlock();

        // this one can be definitely cached forever as the result never changes if the block exists
        Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature);

        Task<BlockAPIResult> GetLastConsolidationBlock();
        Task<MultiBlockAPIResult> GetConsolidationBlocks(string AccountId, string Signature, long startHeight, int count);
        Task<MultiBlockAPIResult> GetBlocksByConsolidation(string AccountId, string Signature, string consolidationHash);
        //Task<GetListStringAPIResult> GetUnConsolidatedBlocks(string AccountId, string Signature);
        // add new api, all upgraded, delete old api, done.
        Task<TransactionsAPIResult> SearchTransactions(string accountId, long startTimeTicks, long endTimeTicks, int count);
        Task<MultiBlockAPIResult> GetBlocksByTimeRange(DateTime startTime, DateTime endTime);
        Task<GetListStringAPIResult> GetBlockHashesByTimeRange(DateTime startTime, DateTime endTime);
        Task<MultiBlockAPIResult> GetBlocksByTimeRange(long startTimeTicks, long endTimeTicks);
        Task<GetListStringAPIResult> GetBlockHashesByTimeRange(long startTimeTicks, long endTimeTicks);
        #endregion Blocklist information methods

        #region Account maintenance methods

        // TO DO add authentication for Account maintenance methods
        // using Diffie-Helman shared secret algorithm with AccountId as a sender's public key and Node's account id as the recipient's public key.
        // This way only account holders can request the account information which will prevent DoS and add some privacy in centralized network configuration. 

        Task<AccountHeightAPIResult> GetAccountHeight(string AccountId);
        Task<BlockAPIResult> GetLastBlock(string AccountId);

        Task<BlockAPIResult> GetBlockByIndex(string AccountId, long Index);
        Task<BlockAPIResult> GetServiceBlockByIndex(string blockType, long Index);

        // Retrives a block by its hash
        Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature);
        Task<BlockAPIResult> GetBlock(string Hash);
        Task<BlockAPIResult> GetBlockBySourceHash(string sourceHash);

        Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature);
        Task<NewTransferAPIResult2> LookForNewTransfer2(string AccountId, string Signature);
        Task<NewFeesAPIResult> LookForNewFees(string AccountId, string Signature);

        Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature);
        #endregion Account maintenance methods

        #region voting
        List<Voter> GetVoters(VoteQueryModel model);
        List<Vote> FindVotes(VoteQueryModel model);
        FeeStats GetFeeStats();
        #endregion

        #region Reward trade methods

        Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature);

        Task<TradeAPIResult> LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature);

        #endregion

        #region Liquidate Pool
        Task<PoolInfoAPIResult> GetPool(string token0, string token1);
        #endregion

    }

    public interface INodeTransactionAPI
    {
        Task<BillBoard> GetBillBoardAsync();
        Task<List<TransStats>> GetTransStatsAsync();
        Task<string> GetDbStats();

        #region Authorization methods 
        // These methods return authorization result and authorizers' signatures if approved

        Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock block);

        Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock block);
        Task<AuthorizationAPIResult> ReceiveFee(ReceiveAuthorizerFeeBlock block);

        Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock block);

        Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block);

        Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock block);

        Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block);

        Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock block);

        #endregion Authorization methods

        #region Reward Trade Athorization Methods
        
        Task<TradeOrderAuthorizationAPIResult> TradeOrder(TradeOrderBlock block);

        Task<AuthorizationAPIResult> Trade(TradeBlock block);

        Task<AuthorizationAPIResult> ExecuteTradeOrder(ExecuteTradeOrderBlock block);

        Task<AuthorizationAPIResult> CancelTradeOrder(CancelTradeOrderBlock block);
        
        #endregion
    }
}
