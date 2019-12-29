using Lyra.Client.Lib;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Exchange;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Client.Lib
{
/*    public class DAGAPIClient : INodeAPI
    {
        private DAGClientHostedService _svc;
        public DAGAPIClient(DAGClientHostedService clientSvc)
        {
            _svc = clientSvc;
        }
        public Task<APIResult> CancelExchangeOrder(string AccountId, string Signature, string cancelKey)
        {
            return _svc.Node.CancelExchangeOrder(AccountId, Signature, cancelKey);
        }

        public Task<AuthorizationAPIResult> CancelTradeOrder(CancelTradeOrderBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<ExchangeAccountAPIResult> CloseExchangeAccount(string AccountId, string Signature)
        {
            return _svc.Node.CloseExchangeAccount(AccountId, Signature);
        }

        public Task<ExchangeAccountAPIResult> CreateExchangeAccount(string AccountId, string Signature)
        {
            return _svc.Node.CreateExchangeAccount(AccountId, Signature);
        }

        public Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock block)
        {
            return _svc.Node.CreateToken(block);
        }

        public Task<AuthorizationAPIResult> ExecuteTradeOrder(ExecuteTradeOrderBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AccountHeightAPIResult> GetAccountHeight(string AccountId, string Signature)
        {
            return _svc.Node.GetAccountHeight(AccountId, Signature);
        }

        public Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            return _svc.Node.GetBlockByHash(AccountId, Hash, Signature);
        }

        public Task<BlockAPIResult> GetBlockByIndex(string AccountId, long Index, string Signature)
        {
            return _svc.Node.GetBlockByIndex(AccountId, Index, Signature);
        }

        public Task<ExchangeBalanceAPIResult> GetExchangeBalance(string AccountId, string Signature)
        {
            return _svc.Node.GetExchangeBalance(AccountId, Signature);
        }

        public Task<BlockAPIResult> GetLastServiceBlock(string AccountId, string Signature)
        {
            return _svc.Node.GetLastServiceBlock(AccountId, Signature);
        }

        public Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
        {
            return _svc.Node.GetNonFungibleTokens(AccountId, Signature);
        }

        public Task<List<ExchangeOrder>> GetOrdersForAccount(string AccountId, string Signature)
        {
            return _svc.Node.GetOrdersForAccount(AccountId, Signature);
        }

        public Task<AccountHeightAPIResult> GetSyncHeight()
        {
            return _svc.Node.GetSyncHeight();
        }

        public Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            return _svc.Node.GetTokenGenesisBlock(AccountId, TokenTicker, Signature);
        }

        public Task<GetTokenNamesAPIResult> GetTokenNames(string AccountId, string Signature, string keyword)
        {
            return _svc.Node.GetTokenNames(AccountId, Signature, keyword);
        }

        public Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion)
        {
            return _svc.Node.GetVersion(apiVersion, appName, appVersion);
        }

        public Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            return _svc.Node.ImportAccount(block);
        }

        public Task<TradeAPIResult> LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            return _svc.Node.LookForNewTrade(AccountId, BuyTokenCode, SellTokenCode, Signature);
        }

        public Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            return _svc.Node.LookForNewTransfer(AccountId, Signature);
        }

        public Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock block)
        {
            return _svc.Node.OpenAccountWithGenesis(block);
        }

        public Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            return _svc.Node.OpenAccountWithImport(block);
        }

        public Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock block)
        {
            return _svc.Node.ReceiveTransfer(block);
        }

        public Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock block)
        {
            return _svc.Node.ReceiveTransferAndOpenAccount(block);
        }

        public Task<APIResult> RequestMarket(string tokenName)
        {
            return _svc.Node.RequestMarket(tokenName);
        }

        public Task<AuthorizationAPIResult> SendExchangeTransfer(ExchangingBlock block)
        {
            return _svc.Node.SendExchangeTransfer(block);
        }

        public Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock block)
        {
            return _svc.Node.SendTransfer(block);
        }

        public Task<CancelKey> SubmitExchangeOrder(TokenTradeOrder order)
        {
            return _svc.Node.SubmitExchangeOrder(order);
        }

        public Task<AuthorizationAPIResult> Trade(TradeBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<TradeOrderAuthorizationAPIResult> TradeOrder(TradeOrderBlock block)
        {
            throw new NotImplementedException();
        }
    }*/
}
