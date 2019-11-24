using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Lyra.Exchange;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lyra.Core.API
{
    public interface INodeAPI
    {
        Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion);

        #region Blocklist information methods
        // this one can be cached for a few milliseconds
        Task<AccountHeightAPIResult> GetSyncHeight();

        Task<GetTokenNamesAPIResult> GetTokenNames(string AccountId, string Signature, string keyword);

        // this one can be cached for a few seconds
        Task<BlockAPIResult> GetLastServiceBlock(string AccountId, string Signature);

        // this one can be definitely cached forever as the result never changes if the block exists
        Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature);

        // Retrives a block by its hash
        //Task<BlockAPIResult> GetBlockByHash(string Hash);

        #endregion Blocklist information methods

        #region Account maintenance methods

        // TO DO add authentication for Account maintenance methods
        // using Diffie-Helman shared secret algorithm with AccountId as a sender's public key and Node's account id as the recipient's public key.
        // This way only account holders can request the account information which will prevent DoS and add some privacy in centralized network configuration. 

        Task<AccountHeightAPIResult> GetAccountHeight(string AccountId, string Signature);

        Task<BlockAPIResult> GetBlockByIndex(string AccountId, int Index, string Signature);

        // Retrives a block by its hash
        Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature);

        Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature);

        Task<TradeAPIResult> LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature);

        Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature);

        Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature);

        //Task<TradeOrderListAPIResult> GetActiveBuyOrders(string BuyToken, string SellToken);
        #endregion Account maintenance methods

        #region Authorization methods 
        // These methods return authorization result and authorizers' signatures if approved

        Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock block);

        Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock block);

        Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock block);

        Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block);

        Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock block);

        Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block);

        Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock block);

        Task<TradeOrderAuthorizationAPIResult> TradeOrder(TradeOrderBlock block);

        Task<AuthorizationAPIResult> Trade(TradeBlock block);

        Task<AuthorizationAPIResult> ExecuteTradeOrder(ExecuteTradeOrderBlock block);

        Task<AuthorizationAPIResult> CancelTradeOrder(CancelTradeOrderBlock block);

        #endregion Authorization methods

        #region Exchange, DEX
        Task<ExchangeAccountAPIResult> CreateExchangeAccount(string AccountId, string Signature);
        Task<CancelKey> SubmitExchangeOrder(TokenTradeOrder order);
        Task<APIResult> CancelExchangeOrder(string AccountId, string Signature, string cancelKey);
        Task<ExchangeBalanceAPIResult> GetExchangeBalance(string AccountId, string Signature);
        Task<ExchangeAccountAPIResult> CloseExchangeAccount(string AccountId, string Signature);
        Task<APIResult> RequestMarket(string tokenName);
        Task<List<ExchangeOrder>> GetOrdersForAccount(string AccountId, string Signature);
        //Task<APIResult> CustomizeNotifySettings(NotifySettings settings);
        #endregion
    }

    public class NotifySettings
    {
        public string AccountID { get; set; }
        public string Signature { get; set; }
        Dictionary<NotifySource, string> SourceConfig { get; set; }
    }
}
