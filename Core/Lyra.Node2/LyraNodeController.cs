using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Lyra.Exchange;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LyraLexWeb2
{
    [Route("api/[controller]")]
    [ApiController]
    public class LyraNodeController : ControllerBase
    {
        private INodeAPI _node;
        public LyraNodeController(INodeAPI node)
        {
            _node = node;
        }
        // GET: api/LyraNode
        [HttpGet]
        public async Task<AccountHeightAPIResult> GetAsync()
        {
            return await _node.GetSyncHeight();
        }

        [Route("GetVersion")]
        public async Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion)
        {
            return await _node.GetVersion(apiVersion, appName, appVersion);
        }

        [Route("GetSyncHeight")]
        public async Task<AccountHeightAPIResult> GetSyncHeightAsync() {
            return await _node.GetSyncHeight();
        }

        [Route("GetTokenNames")]
        public async Task<GetTokenNamesAPIResult> GetTokenNames(string AccountId, string Signature, string keyword)
        {
            return await _node.GetTokenNames(AccountId, Signature, keyword);
        }

        [Route("GetAccountHeight")]
        public async Task<AccountHeightAPIResult> GetAccountHeight(string AccountId, string Signature)
        {
            return await _node.GetAccountHeight(AccountId, Signature);
        }

        [Route("GetBlockByIndex")]
        public async Task<BlockAPIResult> GetBlockByIndex(string AccountId, int Index, string Signature)
        {
            return await _node.GetBlockByIndex(AccountId, Index, Signature);
        }

        [Route("GetBlockByHash")]
        public async Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            return await _node.GetBlockByHash(AccountId, Hash, Signature);
        }

        [Route("GetNonFungibleTokens")]
        public async Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
        {
            return await _node.GetNonFungibleTokens(AccountId, Signature);
        }

        [Route("GetTokenGenesisBlock")]
        public async Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            return await _node.GetTokenGenesisBlock(AccountId, TokenTicker, Signature);
        }

        [Route("GetLastServiceBlock")]
        public async Task<BlockAPIResult> GetLastServiceBlock(string AccountId, string Signature)
        {
            return await _node.GetLastServiceBlock(AccountId, Signature);
        }

        [Route("LookForNewTransfer")]
        public async Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            return await _node.LookForNewTransfer(AccountId, Signature);
        }

        [Route("OpenAccountWithGenesis")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock block)
        {
            return await _node.OpenAccountWithGenesis(block);
        }

        [Route("ReceiveTransferAndOpenAccount")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock openReceiveBlock)
        {
            return await _node.ReceiveTransferAndOpenAccount(openReceiveBlock);
        }

        [Route("OpenAccountWithImport")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            return await _node.OpenAccountWithImport(block);
        }

        [Route("SendTransfer")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock sendBlock)
        {
            return await _node.SendTransfer(sendBlock);
        }

        [Route("SendExchangeTransfer")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> SendExchangeTransfer(ExchangingBlock sendBlock)
        {
            return await _node.SendExchangeTransfer(sendBlock);
        }

        [Route("ReceiveTransfer")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock receiveBlock)
        {
            return await _node.ReceiveTransfer(receiveBlock);
        }

        [Route("ImportAccount")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            return await _node.ImportAccount(block);
        }

        [Route("CreateToken")]
        [HttpPost]
        public async Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock tokenBlock)
        {
            return await _node.CreateToken(tokenBlock);
        }

        [Route("CreateExchangeAccount")]
        public async Task<ExchangeAccountAPIResult> CreateExchangeAccount(string AccountId, string Signature)
        {
            return await _node.CreateExchangeAccount(AccountId, Signature);
        }

        [Route("GetExchangeBalance")]
        public async Task<ExchangeBalanceAPIResult> GetExchangeBalance(string AccountId, string Signature)
        {
            return await _node.GetExchangeBalance(AccountId, Signature);
        }

        [Route("SubmitExchangeOrder")]
        [HttpPost]
        public async Task<CancelKey> SubmitExchangeOrder(TokenTradeOrder order)
        {
            return await _node.SubmitExchangeOrder(order);
        }

        [Route("CancelExchangeOrder")]
        public async Task<APIResult> SubmitExchangeOrder(string AccountId, string Signature, string cancelKey)
        {
            return await _node.CancelExchangeOrder(AccountId, Signature, cancelKey);
        }

        [Route("RequestMarket")]
        public async Task<APIResult> RequestMarket(string TokenName)
        {
            return await _node.RequestMarket(TokenName);
        }

        [Route("GetOrdersForAccount")]
        public async Task<List<ExchangeOrder>> GetOrdersForAccount(string AccountId, string Signature)
        {
            return await _node.GetOrdersForAccount(AccountId, Signature);
        }

        //[HttpPost]
        //public IActionResult Edit(int id, Product product) { ... }

        // GET: api/LyraNode/5
        [HttpGet("{id}", Name = "Get")]
        public string Get(int id)
        {
            return "value";
        }

        // POST: api/LyraNode
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT: api/LyraNode/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE: api/ApiWithActions/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
