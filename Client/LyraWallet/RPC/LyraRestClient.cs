using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LyraWallet.RPC
{
    public class LyraRestClient : INodeAPI
    {
        Task<AuthorizationAPIResult> INodeAPI.CancelTradeOrder(CancelTradeOrderBlock block)
        {
            throw new NotImplementedException();
        }

        Task<AuthorizationAPIResult> INodeAPI.CreateToken(TokenGenesisBlock block)
        {
            throw new NotImplementedException();
        }

        Task<AuthorizationAPIResult> INodeAPI.ExecuteTradeOrder(ExecuteTradeOrderBlock block)
        {
            throw new NotImplementedException();
        }

        Task<AccountHeightAPIResult> INodeAPI.GetAccountHeight(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        Task<ActiveTradeOrdersAPIResult> INodeAPI.GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            throw new NotImplementedException();
        }

        Task<BlockAPIResult> INodeAPI.GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            throw new NotImplementedException();
        }

        Task<BlockAPIResult> INodeAPI.GetBlockByIndex(string AccountId, int Index, string Signature)
        {
            throw new NotImplementedException();
        }

        Task<BlockAPIResult> INodeAPI.GetLastServiceBlock(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        Task<NonFungibleListAPIResult> INodeAPI.GetNonFungibleTokens(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        Task<AccountHeightAPIResult> INodeAPI.GetSyncHeight()
        {
            throw new NotImplementedException();
        }

        Task<BlockAPIResult> INodeAPI.GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            throw new NotImplementedException();
        }

        Task<GetTokenNamesAPIResult> INodeAPI.GetTokenNames(string AccountId, string Signature, string keyword)
        {
            throw new NotImplementedException();
        }

        Task<AuthorizationAPIResult> INodeAPI.ImportAccount(ImportAccountBlock block)
        {
            throw new NotImplementedException();
        }

        Task<TradeAPIResult> INodeAPI.LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            throw new NotImplementedException();
        }

        Task<NewTransferAPIResult> INodeAPI.LookForNewTransfer(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        Task<AuthorizationAPIResult> INodeAPI.OpenAccountWithGenesis(LyraTokenGenesisBlock block)
        {
            throw new NotImplementedException();
        }

        Task<AuthorizationAPIResult> INodeAPI.OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            throw new NotImplementedException();
        }

        Task<AuthorizationAPIResult> INodeAPI.ReceiveTransfer(ReceiveTransferBlock block)
        {
            throw new NotImplementedException();
        }

        Task<AuthorizationAPIResult> INodeAPI.ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock block)
        {
            throw new NotImplementedException();
        }

        Task<AuthorizationAPIResult> INodeAPI.SendTransfer(SendTransferBlock block)
        {
            throw new NotImplementedException();
        }

        Task<AuthorizationAPIResult> INodeAPI.Trade(TradeBlock block)
        {
            throw new NotImplementedException();
        }

        Task<TradeOrderAuthorizationAPIResult> INodeAPI.TradeOrder(TradeOrderBlock block)
        {
            throw new NotImplementedException();
        }
    }
}
