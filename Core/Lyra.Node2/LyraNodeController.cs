using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lyra;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Core.Exchange;
using Lyra.Exchange;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LyraLexWeb2
{
    [Route("api/[controller]")]
    [ApiController]
    public class LyraNodeController : ControllerBase
    {
        INodeAPI _node;
        INodeTransactionAPI _trans;
        DealEngine _dex;
        public LyraNodeController(INodeAPI node,
            INodeTransactionAPI trans
            )
        {
            _node = node;
            _trans = trans;
            _dex = NodeService.Dealer;
        }
        private void CheckSyncState()
        {
            if (BlockChain.Singleton == null)
            {
                throw new Exception("Not fully startup");
            }
        }
        // GET: api/LyraNode
        [HttpGet]
        public async Task<AccountHeightAPIResult> GetAsync()
        {
            CheckSyncState();
            return await _node.GetSyncHeight();
        }

        [Route("GetBillboard")]
        [HttpGet]
        public async Task<BillBoard> GetBillboard()
        {
            CheckSyncState();
            return await _trans.GetBillBoardAsync();
        }

        [Route("GetTransStats")]
        [HttpGet]
        public async Task<List<TransStats>> GetTransStats()
        {
            CheckSyncState();
            return await _trans.GetTransStatsAsync();
        }

        [Route("GetDbStats")]
        [HttpGet]
        public async Task<string> GetDbStats()
        {
            CheckSyncState();
            return await _trans.GetDbStats();
        }

        [Route("GetVersion")]
        [HttpGet]
        public async Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion)
        {
            CheckSyncState();
            return await _node.GetVersion(apiVersion, appName, appVersion);
        }

        [Route("GetSyncState")]
        [HttpGet]
        public async Task<GetSyncStateAPIResult> GetSyncState()
        {
            CheckSyncState();
            return await _node.GetSyncState();
        }

        [Route("GetSyncHeight")]
        [HttpGet]
        public async Task<AccountHeightAPIResult> GetSyncHeightAsync() {
            CheckSyncState();
            return await _node.GetSyncHeight();
        }

        [Route("GetTokenNames")]
        [HttpGet]
        public async Task<GetListStringAPIResult> GetTokenNames(string AccountId, string Signature, string keyword)
        {
            CheckSyncState();
            return await _node.GetTokenNames(AccountId, Signature, keyword);
        }

        [Route("GetAccountHeight")]
        [HttpGet]
        public async Task<AccountHeightAPIResult> GetAccountHeight(string AccountId, string Signature)
        {
            CheckSyncState();
            return await _node.GetAccountHeight(AccountId, Signature);
        }

        [Route("GetBlockByIndex")]
        [HttpGet]
        public async Task<BlockAPIResult> GetBlockByIndex(string AccountId, int Index, string Signature)
        {
            CheckSyncState();
            return await _node.GetBlockByIndex(AccountId, Index, Signature);
        }

        [Route("GetBlockByHash")]
        [HttpGet]
        public async Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            CheckSyncState();
            return await _node.GetBlockByHash(AccountId, Hash, Signature);
        }

        [Route("GetNonFungibleTokens")]
        [HttpGet]
        public async Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
        {
            CheckSyncState();
            return await _node.GetNonFungibleTokens(AccountId, Signature);
        }

        [Route("GetTokenGenesisBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            CheckSyncState();
            return await _node.GetTokenGenesisBlock(AccountId, TokenTicker, Signature);
        }

        [Route("GetLastServiceBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetLastServiceBlock(string AccountId, string Signature)
        {
            CheckSyncState();
            return await _node.GetLastServiceBlock(AccountId, Signature);
        }

        [Route("GetLastConsolidationBlock")]
        [HttpGet]
        public async Task<BlockAPIResult> GetLastConsolidationBlock(string AccountId, string Signature)
        {
            CheckSyncState();
            return await _node.GetLastConsolidationBlock(AccountId, Signature);
        }

        [Route("GetBlocksByConsolidation")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetBlocksByConsolidation(string AccountId, string Signature, string consolidationHash)
        {
            CheckSyncState();
            return await _node.GetBlocksByConsolidation(AccountId, Signature, consolidationHash);
        }

        [Route("GetConsolidationBlocks")]
        [HttpGet]
        public async Task<MultiBlockAPIResult> GetConsolidationBlocks(string AccountId, string Signature, long startHeight)
        {
            CheckSyncState();
            return await _node.GetConsolidationBlocks(AccountId, Signature, startHeight);
        }

        [Route("GetUnConsolidatedBlocks")]
        [HttpGet]
        public async Task<GetListStringAPIResult> GetUnConsolidatedBlocks(string AccountId, string Signature)
        {
            CheckSyncState();
            return await _node.GetUnConsolidatedBlocks(AccountId, Signature);
        }

        [Route("LookForNewTransfer")]
        [HttpGet]
        public async Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            CheckSyncState();
            return await _node.LookForNewTransfer(AccountId, Signature);
        }

        [Route("OpenAccountWithGenesis")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock block)
        {
            CheckSyncState();
            return await _trans.OpenAccountWithGenesis(block);
        }

        [Route("ReceiveTransferAndOpenAccount")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock openReceiveBlock)
        {
            CheckSyncState();
            return await _trans.ReceiveTransferAndOpenAccount(openReceiveBlock);
        }

        [Route("OpenAccountWithImport")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            CheckSyncState();
            return await _trans.OpenAccountWithImport(block);
        }

        [Route("SendTransfer")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock sendBlock)
        {
            CheckSyncState();
            return await _trans.SendTransfer(sendBlock);
        }

        [Route("SendExchangeTransfer")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> SendExchangeTransfer(ExchangingBlock sendBlock)
        {
            CheckSyncState();
            return await _trans.SendExchangeTransfer(sendBlock);
        }

        [Route("ReceiveTransfer")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock receiveBlock)
        {
            CheckSyncState();
            return await _trans.ReceiveTransfer(receiveBlock);
        }

        [Route("ImportAccount")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            CheckSyncState();
            return await _trans.ImportAccount(block);
        }

        [Route("CreateToken")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock tokenBlock)
        {
            CheckSyncState();
            return await _trans.CreateToken(tokenBlock);
        }

        [Route("CreateExchangeAccount")]
        [HttpGet]
        public async Task<ExchangeAccountAPIResult> CreateExchangeAccount(string AccountId, string Signature)
        {
            CheckSyncState();
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
            CheckSyncState();
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
            CheckSyncState();
            var acct = await _dex.GetExchangeAccount(AccountId);
            return await _dex.AddOrderAsync(acct, order);
        }

        [Route("CancelExchangeOrder")]
        [HttpGet]
        public async Task<APIResult> CancelExchangeOrder(string AccountId, string Signature, string cancelKey)
        {
            CheckSyncState();
            await _dex.RemoveOrderAsync(cancelKey);
            return new APIResult { ResultCode = APIResultCodes.Success };
        }

        [Route("RequestMarket")]
        [HttpGet]
        public async Task<APIResult> RequestMarket(string TokenName)
        {
            CheckSyncState();
            await _dex.SendMarket(TokenName);
            return new APIResult { ResultCode = APIResultCodes.Success };
        }

        [Route("GetOrdersForAccount")]
        [HttpGet]
        public async Task<List<ExchangeOrder>> GetOrdersForAccount(string AccountId, string Signature)
        {
            CheckSyncState();
            return await _dex.GetOrdersForAccount(AccountId);
        }

        //[HttpPost]
        //public IActionResult Edit(int id, Product product) { ... }

        // GET: api/LyraNode/5
        //[HttpGet("{id}", Name = "Get")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

        //// POST: api/LyraNode
        //[HttpPost]
        //public void Post([FromBody] string value)
        //{
        //}

        //// PUT: api/LyraNode/5
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
