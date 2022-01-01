using Lyra.Core.Blocks;
using Lyra.Core.API;
using System.Threading.Tasks;


namespace Lyra.Core.Authorizers
{
    public class TradeOrderAuthorizer: TransactionAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is TradeOrderBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as TradeOrderBlock;

            if (block.MaxQuantity != 1)
                return APIResultCodes.FeatureIsNotSupported;

            // 1. check if the account already exists
            if (!await sys.Storage.AccountExistsAsync(block.AccountID))
                return APIResultCodes.AccountDoesNotExist;

            if (await sys.Storage.WasAccountImportedAsync(block.AccountID))
                return APIResultCodes.CannotModifyImportedAccount;

            var lastBlock = await sys.Storage.FindLatestBlockAsync(block.AccountID) as TransactionBlock;
            
            if (lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            //if (lastBlock.Balances[TokenGenesisBlock.POINTGEAR_TICKER_CODE] <= block.Balances[TokenGenesisBlock.POINTGEAR_TICKER_CODE] + block.Fee)
            //    return AuthorizationResultCodes.NegativeTransactionAmount;

            // Validate the destination account id (should be empty)
            if (!string.IsNullOrWhiteSpace(block.DestinationAccountId))
                return APIResultCodes.InvalidDestinationAccountId;

            var result = await VerifyTransactionBlockAsync(sys, block);
            if (result != APIResultCodes.Success)
                return result;

            if (!block.ValidateTransaction(lastBlock))
                return APIResultCodes.TradeOrderValidationFailed;

            var transaction = block.GetTransaction(lastBlock);

            if (block.MinTradeAmount < 0 || block.MinTradeAmount > block.TradeAmount)
                return APIResultCodes.TradeOrderValidationFailed;

            if (block.SellTokenCode != transaction.TokenCode)
                return APIResultCodes.TradeOrderValidationFailed;

            var token = await sys.Storage.FindTokenGenesisBlockAsync(null, block.BuyTokenCode);
            if (token == null)
                return APIResultCodes.TradeOrderValidationFailed;

            bool res = (block.OrderType == TradeOrderTypes.Sell) ? await ValidateSellOrderAsync(sys, block, transaction) : await ValidateBuyOrderAsync(sys, block, transaction);

            if (!res)
                return APIResultCodes.TradeOrderValidationFailed;

            var MatchTradeBlock = await sys.TradeEngine.MatchAsync(block);
            if (MatchTradeBlock != null)
                return APIResultCodes.TradeOrderMatchFound;

            //sys.TradeEngine.AddOrder(block);

            return await MeasureAuthAsync("TradeOrderAuthorizer", "TransactionAuthorizer", base.AuthorizeImplAsync(sys, tblock));
        }

        protected override async Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            if ((block as TradeOrderBlock).CoverAnotherTradersFee && (block as TradeOrderBlock).AnotherTraderWillCoverFee)
                return APIResultCodes.InvalidFeeType;

            if (block.FeeType != AuthorizationFeeTypes.NoFee)
               return APIResultCodes.InvalidFeeType;

            // no fee for the order (it will be paid in executeorder block)
            if (block.Fee != 0)
                return APIResultCodes.InvalidFeeAmount;

            return await Task.FromResult(APIResultCodes.Success);
        }

        private async Task<bool> ValidateSellOrderAsync(DagSystem sys, TradeOrderBlock block, TransactionInfo transaction)
        {
            var serviceblock = await sys.Storage.GetLastServiceBlockAsync();
            decimal balance_change = block.TradeAmount;
            decimal reference_fee = serviceblock.TradeFee;
            if (block.CoverAnotherTradersFee)
                reference_fee = serviceblock.TradeFee * 2;
            else
                if (block.AnotherTraderWillCoverFee)
                    reference_fee = 0;

            if (block.SellTokenCode == LyraGlobal.OFFICIALTICKERCODE)
                balance_change += reference_fee;

            if (transaction.Amount != balance_change)
                return false;

            return true;
        }

        private async Task<bool> ValidateBuyOrderAsync(DagSystem sys, TradeOrderBlock block, TransactionInfoEx transaction)
        {
            var sell_token_genesis_block = await sys.Storage.FindTokenGenesisBlocksAsync(block.SellTokenCode);
            if (sell_token_genesis_block == null)
                return false;

            var buy_token_genesis_block = await sys.Storage.FindTokenGenesisBlocksAsync(block.BuyTokenCode);
            if (sell_token_genesis_block == null)
                return false;

            if (transaction.Amount != block.TradeAmount * block.Price)
                return false;

            var serviceblock = await sys.Storage.GetLastServiceBlockAsync();
            //decimal real_price = Math.Round(block.Price / (decimal)Math.Pow(10, sell_token_precision), sell_token_precision);
            //decimal real_trade_amount = Math.Round(block.TradeAmount / (decimal)Math.Pow(10, buy_token_precision), buy_token_precision);
            //long sell_amount = (long) (real_price * real_trade_amount * (decimal)Math.Pow(10, sell_token_precision));
            decimal balance_change = block.TradeAmount * block.Price;

            decimal reference_fee = serviceblock.TradeFee;

            if (block.CoverAnotherTradersFee)
                reference_fee = serviceblock.TradeFee * 2;

            if (block.AnotherTraderWillCoverFee)
                reference_fee = 0;

            if (block.SellTokenCode == LyraGlobal.OFFICIALTICKERCODE)
                balance_change += reference_fee;

            if (transaction.TotalBalanceChange != balance_change)
                return false;

            return true;
        }

        //private int FindTokenPrecision(DagSystem sys, string token)
        //{
        //    int precision = -1;

        //    // see if we have this already in local storage
        //    var genesisBlock = await sys.Storage.FindTokenGenesisBlocksAsync(token);

        //    if (genesisBlock != null)
        //        precision = (int)genesisBlock.Precision;

        //    return precision;
        //}
    }
}
