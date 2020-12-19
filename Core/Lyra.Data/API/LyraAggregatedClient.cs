using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API
{
    /// <summary>
    /// access all primary nodes to determinate state
    /// </summary>
    public class LyraAggregatedClient : INodeAPI, INodeTransactionAPI, INodeDexAPI
    {

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

        public Task<BlockAPIResult> GetBlock(string Hash)
        {
            throw new NotImplementedException();
        }

        public Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<BlockAPIResult> GetBlockByIndex(string AccountId, long Index)
        {
            throw new NotImplementedException();
        }

        public Task<BlockAPIResult> GetBlockBySourceHash(string sourceHash)
        {
            throw new NotImplementedException();
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

        public Task<BlockAPIResult> GetLastBlock(string AccountId)
        {
            throw new NotImplementedException();
        }

        public Task<BlockAPIResult> GetLastConsolidationBlock()
        {
            throw new NotImplementedException();
        }

        public Task<BlockAPIResult> GetLastServiceBlock()
        {
            throw new NotImplementedException();
        }

        public Task<BlockAPIResult> GetLyraTokenGenesisBlock()
        {
            throw new NotImplementedException();
        }

        public Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<List<ExchangeOrder>> GetOrdersForAccount(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<BlockAPIResult> GetServiceBlockByIndex(string blockType, long Index)
        {
            throw new NotImplementedException();
        }

        public Task<BlockAPIResult> GetServiceGenesisBlock()
        {
            throw new NotImplementedException();
        }

        public Task<AccountHeightAPIResult> GetSyncHeight()
        {
            throw new NotImplementedException();
        }

        public Task<GetSyncStateAPIResult> GetSyncState()
        {
            throw new NotImplementedException();
        }

        public Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            throw new NotImplementedException();
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
