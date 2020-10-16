using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Lyra;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Core.Exchange;
using Lyra.Data.API;
using Lyra.Exchange;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LyraLexWeb2
{
    [Route("api/[controller]")]
    [ApiController]
    public class NodeController : ControllerBase
    {
        DateTime _dtStarted;
        INodeAPI _node;
        INodeTransactionAPI _trans;
        DealEngine _dex;

        ILogger _log;
        public NodeController(
            ILogger<NodeController> logger,
            INodeAPI node,
            INodeTransactionAPI trans
            )
        {
            _log = logger;
            _node = node;
            _trans = trans;
            _dex = NodeService.Dealer;
            _dtStarted = DateTime.Now;
        }
        private async Task<bool> CheckServiceStatusAsync()
        {
            var clientIp = Request.HttpContext.Connection.RemoteIpAddress;
            _log.LogInformation($"WebAPI {DateTime.UtcNow} {clientIp} {Request.Method} {Request.GetDisplayUrl()} {Request.ContentLength}");

            if (NodeService.Dag != null && NodeService.Dag.FullStarted)
            {
                var consensusState = await NodeService.Dag.Consensus.Ask<BlockChainState>(new ConsensusService.AskForState());
                if (consensusState == BlockChainState.Almighty)
                    return true;
            }
            
            return false;
        }
        // GET: api/Node
        [HttpGet]
        public async Task<AccountHeightAPIResult> GetAsync()
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetSyncHeight();
        }

        [Route("GetBillboard")]
        [HttpGet]
        public async Task<BillBoard> GetBillboard()
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.GetBillBoardAsync();
        }

        [Route("GetTransStats")]
        [HttpGet]
        public async Task<List<TransStats>> GetTransStats()
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.GetTransStatsAsync();
        }

        [Route("GetDbStats")]
        [HttpGet]
        public async Task<string> GetDbStats()
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.GetDbStats();
        }

        [Route("GetVersion")]
        [HttpGet]
        public async Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetVersion(apiVersion, appName, appVersion);
        }

        [Route("GetSyncState")]
        [HttpGet]
        public async Task<GetSyncStateAPIResult> GetSyncState()
        {
            // always response to query. nebula need this api.
            //if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetSyncState();
        }

        [Route("GetSyncHeight")]
        [HttpGet]
        public async Task<AccountHeightAPIResult> GetSyncHeightAsync() {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetSyncHeight();
        }

        [Route("GetTokenNames")]
        [HttpGet]
        public async Task<GetListStringAPIResult> GetTokenNames(string AccountId, string Signature, string keyword)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetTokenNames(AccountId, Signature, keyword);
        }

        [Route("GetAccountHeight")]
        [HttpGet]
        public async Task<AccountHeightAPIResult> GetAccountHeight(string AccountId)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetAccountHeight(AccountId);
        }

        [Route("GetLastBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetLastBlock(string AccountId)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetLastBlock(AccountId);
        }

        [Route("GetBlockByIndex")]
        [HttpGet]
        public async Task<BlockAPIResult> GetBlockByIndex(string AccountId, int Index)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetBlockByIndex(AccountId, Index);
        }

        [Route("GetServiceBlockByIndex")]
        [HttpGet]
        public async Task<BlockAPIResult> GetServiceBlockByIndex(string blockType, int Index)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetServiceBlockByIndex(blockType, Index);
        }

        [Route("GetBlockByHash")]
        [HttpGet]
        public async Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetBlockByHash(AccountId, Hash, Signature);
        }

        [Route("GetBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetBlock(string Hash)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetBlock(Hash);
        }

        [Route("GetNonFungibleTokens")]
        [HttpGet]
        public async Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetNonFungibleTokens(AccountId, Signature);
        }

        [Route("GetTokenGenesisBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetTokenGenesisBlock(AccountId, TokenTicker, Signature);
        }

        [Route("GetLastServiceBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetLastServiceBlock()
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetLastServiceBlock();
        }

        [Route("GetServiceGenesisBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetServiceGenesisBlock()
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetServiceGenesisBlock();
        }

        [Route("GetLyraTokenGenesisBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetLyraTokenGenesisBlock()
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetLyraTokenGenesisBlock();
        }

        [Route("GetLastConsolidationBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetLastConsolidationBlock()
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetLastConsolidationBlock();
        }

        [Route("GetBlocksByConsolidation")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetBlocksByConsolidation(string AccountId, string Signature, string consolidationHash)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetBlocksByConsolidation(AccountId, Signature, consolidationHash);
        }

        // this api generate too much data so add some limit later
        [Route("GetBlocksByTimeRange")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetBlocksByTimeRange(DateTime startTime, DateTime endTime)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetBlocksByTimeRange(startTime, endTime);
        }

        // this api generate too much data so add some limit later
        [Route("GetBlockHashesByTimeRange")]
        [HttpGet]
        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRange(DateTime startTime, DateTime endTime)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetBlockHashesByTimeRange(startTime, endTime);
        }

        // this api generate too much data so add some limit later
        [Route("GetBlocksByTimeRange2")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetBlocksByTimeRange2(long startTimeTicks, long endTimeTicks)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetBlocksByTimeRange(startTimeTicks, endTimeTicks);
        }

        // this api generate too much data so add some limit later
        [Route("GetBlockHashesByTimeRange2")]
        [HttpGet]
        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRange2(long startTimeTicks, long endTimeTicks)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetBlockHashesByTimeRange(startTimeTicks, endTimeTicks);
        }

        [Route("GetConsolidationBlocks")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetConsolidationBlocks(string AccountId, string Signature, long startHeight, int count)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetConsolidationBlocks(AccountId, Signature, startHeight, count);
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
        public async Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.LookForNewTransfer(AccountId, Signature);
        }

        [Route("LookForNewFees")]
        [HttpGet]
        public async Task<NewFeesAPIResult> LookForNewFees(string AccountId, string Signature)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.LookForNewFees(AccountId, Signature);
        }

        #region Reward trade methods

        [Route("GetActiveTradeOrders")]
        [HttpGet]
        public async Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.GetActiveTradeOrders(AccountId, SellToken, BuyToken, OrderType, Signature);
        }

        [Route("LookForNewTrade")]
        [HttpGet] 
        public async Task<TradeAPIResult> LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _node.LookForNewTrade(AccountId, BuyTokenCode, SellTokenCode, Signature);
        }

        #endregion

        #region Reward Trade Athorization Methods

        [Route("TradeOrder")]
        [HttpPost] 
        public async Task<TradeOrderAuthorizationAPIResult> TradeOrder(TradeOrderBlock block)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.TradeOrder(block);
        }

        [Route("Trade")]
        [HttpPost] 
        public async Task<AuthorizationAPIResult> Trade(TradeBlock block)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.Trade(block);
        }

        [Route("ExecuteTradeOrder")]
        [HttpPost] 
        public async Task<AuthorizationAPIResult> ExecuteTradeOrder(ExecuteTradeOrderBlock block)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.ExecuteTradeOrder(block);
        }

        [Route("CancelTradeOrder")]
        [HttpPost] 
        public async Task<AuthorizationAPIResult> CancelTradeOrder(CancelTradeOrderBlock block)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.CancelTradeOrder(block);
        }

        #endregion



        [Route("OpenAccountWithGenesis")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock block)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.OpenAccountWithGenesis(block);
        }

        [Route("ReceiveTransferAndOpenAccount")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock openReceiveBlock)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.ReceiveTransferAndOpenAccount(openReceiveBlock);
        }

        [Route("OpenAccountWithImport")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.OpenAccountWithImport(block);
        }

        [Route("SendTransfer")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock sendBlock)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.SendTransfer(sendBlock);
        }

        [Route("SendExchangeTransfer")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> SendExchangeTransfer(ExchangingBlock sendBlock)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.SendExchangeTransfer(sendBlock);
        }

        [Route("ReceiveTransfer")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock receiveBlock)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.ReceiveTransfer(receiveBlock);
        }

        [Route("ReceiveFee")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ReceiveFee(ReceiveAuthorizerFeeBlock receiveBlock)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.ReceiveFee(receiveBlock);
        }

        [Route("ImportAccount")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.ImportAccount(block);
        }

        [Route("CreateToken")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock tokenBlock)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _trans.CreateToken(tokenBlock);
        }

        [Route("CreateExchangeAccount")]
        [HttpGet]
        public async Task<ExchangeAccountAPIResult> CreateExchangeAccount(string AccountId, string Signature)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            var acct = await _dex.AddExchangeAccount(AccountId);
            return new ExchangeAccountAPIResult
            {
                AccountId = acct.AccountId,
                ResultCode = APIResultCodes.Success
            };
        }

        [Route("GetExchangeBalance")]
        [HttpGet]
        public async Task<ExchangeBalanceAPIResult> GetExchangeBalance(string AccountId, string Signature)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            var acct = await _dex.GetExchangeAccount(AccountId, true);
            if(acct == null)
            {
                return new ExchangeBalanceAPIResult { ResultCode = APIResultCodes.AccountDoesNotExist };
            }
            else
                return new ExchangeBalanceAPIResult
                {
                    AccountId = acct.AccountId,
                    Balance = acct?.Balance,
                    ResultCode = APIResultCodes.Success
                };
        }

        [Route("SubmitExchangeOrder")]
        [HttpPost]
        public async Task<CancelKey> SubmitExchangeOrder(string AccountId, TokenTradeOrder order)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            var acct = await _dex.GetExchangeAccount(AccountId);
            return await _dex.AddOrderAsync(acct, order);
        }

        [Route("CancelExchangeOrder")]
        [HttpGet]
        public async Task<APIResult> CancelExchangeOrder(string AccountId, string Signature, string cancelKey)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            await _dex.RemoveOrderAsync(cancelKey);
            return new APIResult { ResultCode = APIResultCodes.Success };
        }

        [Route("RequestMarket")]
        [HttpGet]
        public async Task<APIResult> RequestMarket(string TokenName)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            await _dex.SendMarket(TokenName);
            return new APIResult { ResultCode = APIResultCodes.Success };
        }

        [Route("GetOrdersForAccount")]
        [HttpGet]
        public async Task<List<ExchangeOrder>> GetOrdersForAccount(string AccountId, string Signature)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return await _dex.GetOrdersForAccount(AccountId);
        }

        [Route("GetVoters")]
        [HttpPost]
        public async Task<List<Voter>> GetVotersAsync(VoteQueryModel model)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return _node.GetVoters(model);
        }

        [Route("FindVotes")]
        [HttpPost]
        public async Task<List<Vote>> FindVotesAsync(VoteQueryModel model)
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return _node.FindVotes(model);
        }

        [Route("GetFeeStats")]
        [HttpGet]
        public async Task<FeeStats> GetFeeStatsAsync()
        {
            if (! await CheckServiceStatusAsync()) throw new Exception("System Not Ready.");
            return _node.GetFeeStats();
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
