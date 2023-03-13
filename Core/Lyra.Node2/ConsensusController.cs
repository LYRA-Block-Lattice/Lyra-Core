

using Akka.Actor;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Neo;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LyraLexWeb2
{
    //[ApiExplorerSettings(GroupName = "v1")]
    [Route("api/[controller]")]
    [ApiController]
    public class ConsensusController : ControllerBase
    {
        private readonly DateTime _dtStarted;
        readonly ILogger _log;
        LyraAggregatedClient? _aggClient;
        public ConsensusController(
            ILogger<ConsensusController> logger
            )
        {
            _log = logger;
            _dtStarted = DateTime.Now;
        }

        private async Task<bool> CheckServiceStatusAsync()
        {
            if (NodeService.Dag.Consensus == null)
                return false;

            // create an aggregated client
            var bb = await NodeService.Dag.Consensus.Ask<BillBoard>(new ConsensusService.AskForBillboard());
            _aggClient = new LyraAggregatedClient(Neo.Settings.Default.LyraNode.Lyra.NetworkId, false, NodeService.Dag.PosWallet.AccountId, bb);

            return true;
        }

        // GET: api/Node
        [HttpGet]
        public async Task<AccountHeightAPIResult> GetAsync()
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetSyncHeightAsync();
        }

        [Route("GetBillboard")]
        [HttpGet]
        public async Task<BillBoard> GetBillboardAsync()
        {
            return await _aggClient.GetBillBoardAsync();
        }

        [Route("GetTransStats")]
        [HttpGet]
        public async Task<List<TransStats>> GetTransStatsAsync()
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetTransStatsAsync();
        }

        [Route("GetDbStats")]
        [HttpGet]
        public async Task<string> GetDbStatsAsync()
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetDbStatsAsync();
        }

        [Route("GetVersion")]
        [HttpGet]
        public async Task<GetVersionAPIResult> GetVersionAsync(int apiVersion, string appName, string appVersion)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetVersionAsync(apiVersion, appName, appVersion);
        }

        //[Route("GetThumbPrint")]
        //[HttpGet]
        //public async Task<string> GetThumbPrint()
        //{
        //    var ks = Startup.App.ApplicationServices.GetService(typeof(IServer)) as KestrelServer;
        //    var kso = ks.Options;

        //    PropertyInfo prop =
        //        typeof(KestrelServerOptions).GetProperty("DefaultCertificate", BindingFlags.NonPublic | BindingFlags.Instance);

        //    MethodInfo getter = prop.GetGetMethod(nonPublic: true);
        //    var cert = getter.Invoke(kso, null) as X509Certificate2;
        //    if (cert != null)
        //        return cert.Thumbprint;
        //    else
        //        return null;
        //}

        [Route("GetSyncState")]
        [HttpGet]
        public async Task<GetSyncStateAPIResult> GetSyncStateAsync()
        {
            // always response to query. nebula need this api.
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetSyncStateAsync();
        }

        [Route("GetSyncHeight")]
        [HttpGet]
        public async Task<AccountHeightAPIResult> GetSyncHeightAsync() {
            // always response to query. node bootstrap need this api.
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetSyncHeightAsync();
        }

        [Route("GetTokenNames")]
        [HttpGet]
        public async Task<GetListStringAPIResult> GetTokenNamesAsync(string? AccountId, string? Signature, string keyword)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetTokenNamesAsync(AccountId, Signature, keyword);
        }

        [Route("GetAccountHeight")]
        [HttpGet]
        public async Task<AccountHeightAPIResult> GetAccountHeightAsync(string AccountId)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetAccountHeightAsync(AccountId);
        }

        [Route("GetLastBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetLastBlockAsync(string AccountId)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetLastBlockAsync(AccountId);
        }

        [Route("GetBlockByIndex")]
        [HttpGet]
        public async Task<BlockAPIResult> GetBlockByIndexAsync(string AccountId, int Index)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetBlockByIndexAsync(AccountId, Index);
        }

        [Route("GetServiceBlockByIndex")]
        [HttpGet]
        public async Task<BlockAPIResult> GetServiceBlockByIndexAsync(string blockType, int Index)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetServiceBlockByIndexAsync(blockType, Index);
        }

        [Route("GetBlockByHash")]
        [HttpGet]
        public async Task<BlockAPIResult> GetBlockByHashAsync(string AccountId, string Hash, string? Signature)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetBlockByHashAsync(AccountId, Hash, Signature);
        }

        [Route("GetBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetBlockAsync(string Hash)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetBlockAsync(Hash);
        }

        [Route("GetBlockBySourceHash")]
        [HttpGet]
        public async Task<BlockAPIResult> GetBlockBySourceHashAsync(string Hash)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetBlockBySourceHashAsync(Hash);
        }

        [Route("GetBlocksByRelatedTx")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetBlocksByRelatedTxAsync(string Hash)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetBlocksByRelatedTxAsync(Hash);
        }

        [Route("GetNonFungibleTokens")]
        [HttpGet]
        public async Task<NonFungibleListAPIResult> GetNonFungibleTokensAsync(string AccountId, string Signature)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetNonFungibleTokensAsync(AccountId, Signature);
        }

        [Route("GetTokenGenesisBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetTokenGenesisBlockAsync(string AccountId, string TokenTicker, string Signature)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetTokenGenesisBlockAsync(AccountId, TokenTicker, Signature);
        }

        [Route("GetLastServiceBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetLastServiceBlockAsync()
        {
            // always response to query. node bootstrap need this api.
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetLastServiceBlockAsync();
        }

        [Route("GetServiceGenesisBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetServiceGenesisBlockAsync()
        {
            // always response to query. node bootstrap need this api.
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetServiceGenesisBlockAsync();
        }

        [Route("GetLyraTokenGenesisBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetLyraTokenGenesisBlockAsync()
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetLyraTokenGenesisBlockAsync();
        }

        [Route("GetLastConsolidationBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetLastConsolidationBlockAsync()
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetLastConsolidationBlockAsync();
        }

        [Route("GetBlocksByConsolidation")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetBlocksByConsolidationAsync(string AccountId, string Signature, string consolidationHash)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetBlocksByConsolidationAsync(AccountId, Signature, consolidationHash);
        }

        // this api generate too much data so add some limit later
        [Route("GetBlocksByTimeRange")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetBlocksByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetBlocksByTimeRangeAsync(startTime, endTime);
        }

        // this api generate too much data so add some limit later
        [Route("GetBlockHashesByTimeRange")]
        [HttpGet]
        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetBlockHashesByTimeRangeAsync(startTime, endTime);
        }

        [Route("SearchTransactions")]
        [HttpGet]
        public async Task<TransactionsAPIResult> SearchTransactionsAsync(string accountId, long startTimeTicks, long endTimeTicks, int count)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.SearchTransactionsAsync(accountId, startTimeTicks, endTimeTicks, count);
        }

        // this api generate too much data so add some limit later
        [Route("GetBlocksByTimeRange2")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetBlocksByTimeRange2Async(long startTimeTicks, long endTimeTicks)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetBlocksByTimeRangeAsync(startTimeTicks, endTimeTicks);
        }

        // this api generate too much data so add some limit later
        [Route("GetBlockHashesByTimeRange2")]
        [HttpGet]
        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRange2Async(long startTimeTicks, long endTimeTicks)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetBlockHashesByTimeRangeAsync(startTimeTicks, endTimeTicks);
        }

        [Route("GetConsolidationBlocks")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetConsolidationBlocksAsync(string AccountId, string? Signature, long startHeight, int count)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetConsolidationBlocksAsync(AccountId, Signature, startHeight, count);
        }

        //[Route("GetUnConsolidatedBlocks")]
        //[HttpGet]
        //public async Task<GetListStringAPIResult> GetUnConsolidatedBlocks(string AccountId, string Signature)
        //{
        //    CheckSyncState();
        //    return await _node.GetUnConsolidatedBlocks(AccountId, Signature);
        //}

        [Route("LookForNewTransfer")]
        [HttpGet]
        public async Task<NewTransferAPIResult> LookForNewTransferAsync(string AccountId, string Signature)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.LookForNewTransferAsync(AccountId, Signature);
        }

        [Route("LookForNewTransfer2")]
        [HttpGet]
        public async Task<NewTransferAPIResult2> LookForNewTransfer2Async(string AccountId, string Signature)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.LookForNewTransfer2Async(AccountId, Signature);
        }

        [Route("LookForNewFees")]
        [HttpGet]
        public async Task<NewFeesAPIResult> LookForNewFeesAsync(string AccountId, string Signature)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.LookForNewFeesAsync(AccountId, Signature);
        }

        /*
        #region Reward trade methods

        [Route("GetActiveTradeOrders")]
        [HttpGet]
        public async Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrdersAsync(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            if (! await CheckServiceStatus()) return null;
            return await _node.GetActiveTradeOrdersAsync(AccountId, SellToken, BuyToken, OrderType, Signature);
        }

        [Route("LookForNewTrade")]
        [HttpGet] 
        public async Task<TradeAPIResult> LookForNewTradeAsync(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            if (! await CheckServiceStatus()) return null;
            return await _node.LookForNewTradeAsync(AccountId, BuyTokenCode, SellTokenCode, Signature);
        }

        #endregion

        #region Reward Trade Athorization Methods

        [Route("TradeOrder")]
        [HttpPost] 
        public async Task<TradeOrderAuthorizationAPIResult> TradeOrderAsync(TradeOrderBlock block)
        {
            if (! await CheckServiceStatus()) return null;
            return await _node.TradeOrderAsync(block);
        }

        [Route("Trade")]
        [HttpPost] 
        public async Task<AuthorizationAPIResult> TradeAsync(TradeBlock block)
        {
            if (! await CheckServiceStatus()) return null;
            return await _node.TradeAsync(block);
        }

        [Route("ExecuteTradeOrder")]
        [HttpPost] 
        public async Task<AuthorizationAPIResult> ExecuteTradeOrderAsync(ExecuteTradeOrderBlock block)
        {
            if (! await CheckServiceStatus()) return null;
            return await _node.ExecuteTradeOrderAsync(block);
        }

        [Route("CancelTradeOrder")]
        [HttpPost] 
        public async Task<AuthorizationAPIResult> CancelTradeOrderAsync(CancelTradeOrderBlock block)
        {
            if (! await CheckServiceStatus()) return null;
            return await _node.CancelTradeOrderAsync(block);
        }
        
        #endregion
        */



        [Route("OpenAccountWithGenesis")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> OpenAccountWithGenesisAsync(LyraTokenGenesisBlock block)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.OpenAccountWithGenesisAsync(block);
        }

        [Route("ReceiveTransferAndOpenAccount")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccountAsync(OpenWithReceiveTransferBlock openReceiveBlock)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.ReceiveTransferAndOpenAccountAsync(openReceiveBlock);
        }

        [Route("OpenAccountWithImport")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> OpenAccountWithImportAsync(OpenAccountWithImportBlock block)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.OpenAccountWithImportAsync(block);
        }

        [Route("SendTransfer")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> SendTransferAsync(SendTransferBlock sendBlock)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.SendTransferAsync(sendBlock);
        }

        [Route("ReceiveTransfer")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ReceiveTransferAsync(ReceiveTransferBlock receiveBlock)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.ReceiveTransferAsync(receiveBlock);
        }

        [Route("ReceiveFee")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ReceiveFeeAsync(ReceiveNodeProfitBlock receiveBlock)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.ReceiveFeeAsync(receiveBlock);
        }

        [Route("ImportAccount")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ImportAccountAsync(ImportAccountBlock block)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.ImportAccountAsync(block);
        }

        [Route("CreateToken")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> CreateTokenAsync(TokenGenesisBlock tokenBlock)
        {
            if (! await CheckServiceStatusAsync()) return null;
            return await _aggClient.CreateTokenAsync(tokenBlock);
        }

        //[Route("CreateExchangeAccount")]
        //[HttpGet]
        //public async Task<ExchangeAccountAPIResult> CreateExchangeAccount(string AccountId, string Signature)
        //{
        //    if (! await CheckServiceStatusAsync()) return null;
        //    var acct = await _dex.AddExchangeAccount(AccountId);
        //    return new ExchangeAccountAPIResult
        //    {
        //        AccountId = acct.AccountId,
        //        ResultCode = APIResultCodes.Success
        //    };
        //}

        //[Route("GetExchangeBalance")]
        //[HttpGet]
        //public async Task<ExchangeBalanceAPIResult> GetExchangeBalance(string AccountId, string Signature)
        //{
        //    if (! await CheckServiceStatusAsync()) return null;
        //    var acct = await _dex.GetExchangeAccount(AccountId, true);
        //    if(acct == null)
        //    {
        //        return new ExchangeBalanceAPIResult { ResultCode = APIResultCodes.AccountDoesNotExist };
        //    }
        //    else
        //        return new ExchangeBalanceAPIResult
        //        {
        //            AccountId = acct.AccountId,
        //            Balance = acct?.Balance,
        //            ResultCode = APIResultCodes.Success
        //        };
        //}

        //[Route("SubmitExchangeOrder")]
        //[HttpPost]
        //public async Task<CancelKey> SubmitExchangeOrder(string AccountId, TokenTradeOrder order)
        //{
        //    if (! await CheckServiceStatusAsync()) return null;
        //    var acct = await _dex.GetExchangeAccount(AccountId);
        //    return await _dex.AddOrderAsync(acct, order);
        //}

        //[Route("CancelExchangeOrder")]
        //[HttpGet]
        //public async Task<APIResult> CancelExchangeOrder(string AccountId, string Signature, string cancelKey)
        //{
        //    if (! await CheckServiceStatusAsync()) return null;
        //    await _dex.RemoveOrderAsync(cancelKey);
        //    return new APIResult { ResultCode = APIResultCodes.Success };
        //}

        //[Route("RequestMarket")]
        //[HttpGet]
        //public async Task<APIResult> RequestMarket(string TokenName)
        //{
        //    if (! await CheckServiceStatusAsync()) return null;
        //    await _dex.SendMarket(TokenName);
        //    return new APIResult { ResultCode = APIResultCodes.Success };
        //}

        //[Route("GetOrdersForAccount")]
        //[HttpGet]
        //public async Task<List<ExchangeOrder>> GetOrdersForAccount(string AccountId, string Signature)
        //{
        //    if (! await CheckServiceStatusAsync()) return null;
        //    return await _dex.GetOrdersForAccount(AccountId);
        //}

        //[Route("GetVoters")]
        //[HttpPost]
        //public List<Voter> GetVoters(VoteQueryModel model)
        //{
        //    if (!await CheckServiceStatus()) return null;
        //    return _node.GetVoters(model);
        //}

        //[Route("FindVotes")]
        //[HttpPost]
        //public List<Vote> FindVotes(VoteQueryModel model)
        //{
        //    if (!await CheckServiceStatus()) return null;
        //    return _node.FindVotes(model);
        //}

        //[Route("GetFeeStats")]
        //[HttpGet]
        //public FeeStats GetFeeStats()
        //{
        //    if (!await CheckServiceStatus()) return null;
        //    return _node.GetFeeStats();
        //}

        [Route("GetPool")]
        [HttpGet]
        public async Task<PoolInfoAPIResult> GetPoolAsync(string token0, string token1)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetPoolAsync(token0, token1);
        }

        [Route("GetAllBrokerAccountsForOwner")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetAllBrokerAccountsForOwnerAsync(string ownerAccount)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetAllBrokerAccountsForOwnerAsync(ownerAccount);
        }

        [Route("FindAllProfitingAccounts")]
        [HttpGet]
        public async Task<List<Profiting>> FindAllProfitingAccountsAsync(long timeBeginTicks, long timeEndTicks)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.FindAllProfitingAccountsAsync(new DateTime(timeBeginTicks, DateTimeKind.Utc),
                new DateTime(timeEndTicks, DateTimeKind.Utc));
        }

        [Route("FindProfitingAccountsByName")]
        [HttpGet]
        public async Task<ProfitingGenesis> FindProfitingAccountsByNameAsync(string name)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.FindProfitingAccountsByNameAsync(name);
        }

        //[Route("FindAllStakings")]
        //[HttpGet]
        //public List<Staker> FindAllStakings(string pftid, long timeBeforeTicks)
        //{
        //    if (!await CheckServiceStatus()) return null;
        //    return _node.FindAllStakings(pftid, new DateTime(timeBeforeTicks, DateTimeKind.Utc));
        //}

        [Route("FindAllStakings2")]
        [HttpGet]
        public async Task<SimpleJsonAPIResult> FindAllStakingsAsync(string pftid, long timeBeforeTicks)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.FindAllStakingsAsync(pftid, new DateTime(timeBeforeTicks, DateTimeKind.Utc));
        }

        //[Route("GetAccountStats")]
        //[HttpGet]
        //public async Task<ProfitingStats> GetAccountStatsAsync(string accountId, long timeBeginTicks, long timeEndTicks)
        //{
        //    if (!await CheckServiceStatus()) return null;
        //    return await _node.GetAccountStatsAsync(accountId, new DateTime(timeBeginTicks, DateTimeKind.Utc),
        //        new DateTime(timeEndTicks, DateTimeKind.Utc));
        //}

        [Route("GetBenefitStats")]
        [HttpGet]
        public async Task<ProfitingStats> GetBenefitStatsAsync(string pftid, string stkid, long timeBeginTicks, long timeEndTicks)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetBenefitStatsAsync(pftid, stkid, new DateTime(timeBeginTicks, DateTimeKind.Utc),
                new DateTime(timeEndTicks, DateTimeKind.Utc));
        }

        [Route("GetPendingStats")]
        [HttpGet]
        public async Task<PendingStats> GetPendingStatsAsync(string accountId)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetPendingStatsAsync(accountId);
        }

        // DEX
        [Route("GetAllDexWallets")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetAllDexWalletsAsync(string owner)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetAllDexWalletsAsync(owner);
        }

        [Route("FindDexWallet")]
        [HttpGet]
        public async Task<BlockAPIResult> FindDexWalletAsync(string owner, string symbol, string provider)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.FindDexWalletAsync(owner, symbol, provider);
        }

        [Route("GetAllDaos")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetAllDaosAsync(int page, int pageSize)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetAllDaosAsync(page, pageSize);
        }

        [Route("GetDaoByName")]
        [HttpGet]
        public async Task<BlockAPIResult> GetDaoByNameAsync(string name)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetDaoByNameAsync(name);
        }

        [Route("GetOtcOrdersByOwner")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetOtcOrdersByOwnerAsync(string accountId)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetOtcOrdersByOwnerAsync(accountId);
        }

        [Route("FindTradableOtc")]
        [HttpGet]
        public async Task<ContainerAPIResult> FindTradableOrdersAsync()
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.FindTradableOrdersAsync();
        }

        [Route("FindOtcTrade")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> FindOtcTradeAsync(string accountId, bool onlyOpenTrade, int page, int pageSize)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.FindOtcTradeAsync(accountId, onlyOpenTrade, page, pageSize);
        }

        [Route("FindOtcTradeByStatus")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> FindOtcTradeByStatusAsync(string daoid, OTCTradeStatus status, int page, int pageSize)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.FindOtcTradeByStatusAsync(daoid, status, page, pageSize);
        }

        [Route("GetOtcTradeStatsForUsers")]
        [HttpPost]
        public async Task<SimpleJsonAPIResult> GetOtcTradeStatsForUsersAsync(TradeStatsReq req)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetOtcTradeStatsForUsersAsync(req);
        }        

        [Route("FindAllVotesByDao")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> FindAllVotesByDaoAsync(string daoid, bool openOnly)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.FindAllVotesByDaoAsync(daoid, openOnly);
        }

        [Route("FindAllVoteForTrade")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> FindAllVoteForTradeAsync(string tradeid)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.FindAllVoteForTradeAsync(tradeid);
        }

        [Route("GetVoteSummary")]
        [HttpGet]
        public async Task<SimpleJsonAPIResult> GetVoteSummaryAsync(string voteid)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetVoteSummaryAsync(voteid);
        }

        [Route("FindExecForVote")]
        [HttpGet]
        public async Task<BlockAPIResult> FindExecForVoteAsync(string voteid)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.FindExecForVoteAsync(voteid);
        }

        [Route("GetDealerByAccountId")]
        [HttpGet]
        public async Task<BlockAPIResult> GetDealerByAccountIdAsync(string accountId)
        {
            if (!await CheckServiceStatusAsync()) return null;
            return await _aggClient.GetDealerByAccountIdAsync(accountId);
        }

        //[HttpPost]
        //public IActionResult Edit(int id, Product product) { ... }

        // GET: api/Node/5
        //[HttpGet("{id}", Name = "Get")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

        //// POST: api/Node
        //[HttpPost]
        //public void Post([FromBody] string value)
        //{
        //}

        //// PUT: api/Node/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE: api/ApiWithActions/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}
