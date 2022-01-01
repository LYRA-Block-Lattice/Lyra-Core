using Lyra.Core.Blocks;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class CancelTradeOrderAuthorizer: ReceiveTransferAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is CancelTradeOrderBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as CancelTradeOrderBlock;

            // 1. check if the account exists
            if (!await sys.Storage.AccountExistsAsync(block.AccountID))
                return APIResultCodes.AccountDoesNotExist;

            var lastBlock = await sys.Storage.FindLatestBlockAsync(block.AccountID) as TransactionBlock;
            if (lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            var result = await VerifyTransactionBlockAsync(sys, block);
            if (result != APIResultCodes.Success)
                return result;

            var original_order = await sys.Storage.FindBlockByHashAsync(block.AccountID, block.TradeOrderId) as TradeOrderBlock;
            if (original_order == null)
                return APIResultCodes.NoTradesFound;

            result = await ValidateCancellationBalanceAsync(sys, block, lastBlock, original_order);
            if (result != APIResultCodes.Success)
                return result;

            //sys.TradeEngine.RemoveOrder(original_order);

            return await MeasureAuthAsync("CancelTradeOrderAuthorizer", "ReceiveTransferAuthorizer", base.AuthorizeImplAsync(sys, tblock));
        }

        // The cancellation should restore the balance that was locked by the trade order.
        // Thus, it should take the balance from the latest block and add the balamce (transactin amount) locked by the order block.
        private async Task<APIResultCodes> ValidateCancellationBalanceAsync(DagSystem sys, CancelTradeOrderBlock block, TransactionBlock lastBlock, TradeOrderBlock original_order)
        {
            var order_previous_block = await sys.Storage.FindBlockByHashAsync(original_order.PreviousHash) as TransactionBlock;

            var order_transaction = original_order.GetTransaction(order_previous_block);
            var cancel_transaction = block.GetTransaction(lastBlock);

            if (order_transaction.TotalBalanceChange != cancel_transaction.TotalBalanceChange || order_transaction.TokenCode != cancel_transaction.TokenCode)
                return APIResultCodes.CancelTradeOrderValidationFailed;

            return APIResultCodes.Success;
        }

    }
}
