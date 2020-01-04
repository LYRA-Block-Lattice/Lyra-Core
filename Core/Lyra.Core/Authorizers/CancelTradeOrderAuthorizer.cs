using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;

namespace Lyra.Core.Authorizers
{
    public class CancelTradeOrderAuthorizer: BaseAuthorizer
    {
        public CancelTradeOrderAuthorizer()
        {
            
        }

        public override Task<(APIResultCodes, AuthorizationSignature)> Authorize<T>(T tblock)
        {
            var result = AuthorizeImpl(tblock);
            if (APIResultCodes.Success == result)
                return Task.FromResult((APIResultCodes.Success, Sign(tblock)));
            else
                return Task.FromResult((result, (AuthorizationSignature)null));
        }
        private APIResultCodes AuthorizeImpl<T>(T tblock)
        {
            if (!(tblock is CancelTradeOrderBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as CancelTradeOrderBlock;

            // 1. check if the account exists
            if (!BlockChain.Singleton.AccountExists(block.AccountID))
                return APIResultCodes.AccountDoesNotExist;

            TransactionBlock lastBlock = BlockChain.Singleton.FindLatestBlock(block.AccountID);
            if (lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            var result = VerifyBlock(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            result = VerifyTransactionBlock(block);
            if (result != APIResultCodes.Success)
                return result;

            var original_order = BlockChain.Singleton.FindBlockByHash(block.AccountID, block.TradeOrderId) as TradeOrderBlock;
            if (original_order == null)
                return APIResultCodes.NoTradesFound;

            result = ValidateCancellationBalance(block, lastBlock, original_order);
            if (result != APIResultCodes.Success)
                return result;

            return APIResultCodes.Success;
        }

        // The cancellation should restore the balance that was locked by the trade order.
        // Thus, it should take the balance from the latest block and add the balamce (transactin amount) locked by the order block.
        APIResultCodes ValidateCancellationBalance(CancelTradeOrderBlock block, TransactionBlock lastBlock, TradeOrderBlock original_order)
        {
            var order_previous_block = BlockChain.Singleton.FindBlockByHash(original_order.PreviousHash);

            var order_transaction = original_order.GetTransaction(order_previous_block);
            var cancel_transaction = block.GetTransaction(lastBlock);

            if (order_transaction.TotalBalanceChange != cancel_transaction.TotalBalanceChange || order_transaction.TokenCode != cancel_transaction.TokenCode)
                return APIResultCodes.CancelTradeOrderValidationFailed;

            return APIResultCodes.Success;
        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            if (block.FeeType != AuthorizationFeeTypes.NoFee)
                return APIResultCodes.InvalidFeeAmount;

            if (block.Fee != 0)
                return APIResultCodes.InvalidFeeAmount;

            return APIResultCodes.Success;
        }

    }
}
