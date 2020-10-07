using Lyra.Core.Blocks;
using Lyra.Core.API;
using System.Threading.Tasks;
using Lyra.Data.Crypto;

namespace Lyra.Core.Authorizers
{
    public class ExecuteTradeOrderAuthorizer: SendTransferAuthorizer
    {
        public ExecuteTradeOrderAuthorizer()
        { 
        }

        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var result = await AuthorizeImplAsync(sys, tblock);
            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }

        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is ExecuteTradeOrderBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ExecuteTradeOrderBlock;

            // 1. check if the account already exists
            if (!await sys.Storage.AccountExistsAsync(block.AccountID))
                return APIResultCodes.AccountDoesNotExist;

            if (await sys.Storage.WasAccountImportedAsync(block.AccountID))
                return APIResultCodes.CannotModifyImportedAccount;

            var lastBlock = await sys.Storage.FindLatestBlockAsync(block.AccountID) as TransactionBlock;

            if (lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            var result = await VerifyBlockAsync(sys, block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            // Validate the destination account id
            if (!Signatures.ValidateAccountId(block.DestinationAccountId))
                return APIResultCodes.InvalidDestinationAccountId;

            result = await VerifyTransactionBlockAsync(sys, block);
            if (result != APIResultCodes.Success)
                return result;

            if (!block.ValidateTransaction(lastBlock))
                    return APIResultCodes.ExceptionInExecuteTradeOrderAuthorizer;

            // To DO validate the transaction amount (that it matches the trade amd the order)

            result = await ValidateNonFungibleAsync(sys, block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            // Validate the linked trade
            var trade = await sys.Storage.FindBlockByHashAsync(block.DestinationAccountId, block.TradeId) as TradeBlock;
            if (trade == null)
                return APIResultCodes.NoTradesFound;

            if (block.SellTokenCode != trade.BuyTokenCode)
                return APIResultCodes.TransactionTokenDoesNotMatch;

            // TO DO validate amounts match

            // Validate the linked trade
            var trade_order = await sys.Storage.FindBlockByHashAsync(block.AccountID, block.TradeOrderId) as TradeOrderBlock;
            if (trade_order == null)
                return APIResultCodes.TradeOrderNotFound;

            if (block.SellTokenCode != trade_order.SellTokenCode)
                return APIResultCodes.TransactionTokenDoesNotMatch;

            // TO DO validate amounts match

            //// Deactivate the order if it allows only one matching trade
            //if (trade_order.MaxQuantity == 1)
            //    sys.TradeEngine.RemoveOrder(trade_order);

            return APIResultCodes.Success;

        }

        protected override async Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            var order = await sys.Storage.FindBlockByHashAsync((block as ExecuteTradeOrderBlock).TradeOrderId) as TradeOrderBlock;
            if (order == null)
                return APIResultCodes.NoTradesFound;

            if (order.CoverAnotherTradersFee)
            {
                if (block.FeeType != AuthorizationFeeTypes.BothParties)
                    return APIResultCodes.InvalidFeeAmount;
                if (block.Fee != (await sys.Storage.GetLastServiceBlockAsync()).TradeFee * 2)
                    return APIResultCodes.InvalidFeeAmount;
            }
            else
            if (order.AnotherTraderWillCoverFee)
            {
                if (block.FeeType == AuthorizationFeeTypes.NoFee)
                    return APIResultCodes.InvalidFeeAmount;
                if (block.Fee != 0)
                    return APIResultCodes.InvalidFeeAmount;
            }
            else
            {

                if (block.FeeType == AuthorizationFeeTypes.Regular)
                    return APIResultCodes.InvalidFeeAmount;
                if (block.Fee != (await sys.Storage.GetLastServiceBlockAsync()).TradeFee)
                    return APIResultCodes.InvalidFeeAmount;
            }

            return APIResultCodes.Success;
        }

    }
}

